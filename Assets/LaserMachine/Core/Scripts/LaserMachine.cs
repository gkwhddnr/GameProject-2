using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Lightbug.LaserMachine
{
    public class LaserMachine : MonoBehaviour
    {
        struct LaserElement
        {
            public Transform transform;
            public LineRenderer lineRenderer;
            public GameObject sparks;
        };

        List<LaserElement> elementsList = new List<LaserElement>();

        [Header("External Data")]
        [SerializeField] LaserData m_data;
        [SerializeField] bool m_overrideExternalProperties = true;
        [SerializeField] LaserProperties m_inspectorProperties = new LaserProperties();

        [Header("턴제식 회전")]
        public bool m_rotateOnPlayerTurn = false;

        [Header("회전 설정")]
        public float m_rotationPerTurn = 60f;
        public bool m_smoothTurnRotation = true;
        public float m_turnRotationSpeed = 180f;

        [Header("기본 위치 설정")]
        [SerializeField] private Vector3 m_baseRotation = new Vector3(-90f, 90f, -90f);

        LaserProperties m_currentProperties;
        float m_time = 0;
        bool m_active = true;
        bool m_assignLaserMaterial;
        bool m_assignSparks;

        private float m_targetTurnAngle = 0f;
        private float m_currentTurnAngle = 0f;
        private bool m_isRotatingTurn = false;
        private bool m_eventSubscribed = false;
        private Vector3 m_initialPosition;

        void OnEnable()
        {
            m_initialPosition = transform.position;
            transform.rotation = Quaternion.Euler(m_baseRotation);

            m_currentTurnAngle = 0f;
            m_targetTurnAngle = 0f;

            m_currentProperties = m_overrideExternalProperties ? m_inspectorProperties : m_data.m_properties;
            m_currentProperties.m_initialTimingPhase = Mathf.Clamp01(m_currentProperties.m_initialTimingPhase);
            m_time = m_currentProperties.m_initialTimingPhase * m_currentProperties.m_intervalTime;

            float angleStep = m_currentProperties.m_angularRange / m_currentProperties.m_raysNumber;
            m_assignSparks = m_data.m_laserSparks != null;
            m_assignLaserMaterial = m_data.m_laserMaterial != null;

            foreach (var el in elementsList) if (el.transform != null) Destroy(el.transform.gameObject);
            elementsList.Clear();

            for (int i = 0; i < m_currentProperties.m_raysNumber; i++)
            {
                LaserElement element = new LaserElement();
                GameObject newObj = new GameObject("lineRenderer_" + i.ToString());
                newObj.transform.position = transform.position;
                newObj.transform.rotation = transform.rotation;
                newObj.transform.Rotate(Vector3.up, i * angleStep);
                newObj.transform.position += newObj.transform.forward * m_currentProperties.m_minRadialDistance;
                newObj.transform.SetParent(this.transform);

                newObj.AddComponent<LineRenderer>();
                if (m_assignLaserMaterial) newObj.GetComponent<LineRenderer>().material = m_data.m_laserMaterial;

                LineRenderer lr = newObj.GetComponent<LineRenderer>();
                lr.receiveShadows = false;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.startWidth = m_currentProperties.m_rayWidth;
                lr.useWorldSpace = true;

                if (m_assignSparks)
                {
                    GameObject sparks = Instantiate(m_data.m_laserSparks);
                    sparks.transform.SetParent(newObj.transform);
                    sparks.SetActive(false);
                    element.sparks = sparks;
                }

                element.transform = newObj.transform;
                element.lineRenderer = lr;
                elementsList.Add(element);
            }

            if (m_rotateOnPlayerTurn) TrySubscribeToGameManager();
        }

        void Start()
        {
            if (!m_eventSubscribed && m_rotateOnPlayerTurn) StartCoroutine(WaitForGameManagerAndSubscribe());
        }

        IEnumerator WaitForGameManagerAndSubscribe()
        {
            while (GameManager.Instance == null) yield return new WaitForSeconds(0.1f);
            TrySubscribeToGameManager();
        }

        void TrySubscribeToGameManager()
        {
            if (!m_rotateOnPlayerTurn || m_eventSubscribed || GameManager.Instance == null) return;
            GameManager.Instance.OnPlayerTurnEnd += OnPlayerTurnEnd;
            m_eventSubscribed = true;
        }

        void OnDisable()
        {
            if (GameManager.Instance != null && m_eventSubscribed)
            {
                GameManager.Instance.OnPlayerTurnEnd -= OnPlayerTurnEnd;
                m_eventSubscribed = false;
            }
        }

        private void OnPlayerTurnEnd() { if (m_rotateOnPlayerTurn) { m_targetTurnAngle += m_rotationPerTurn; m_isRotatingTurn = true; } }

        void Update()
        {
            transform.position = m_initialPosition;
            float finalRotationDelta = 0f;

            if (m_rotateOnPlayerTurn)
            {
                if (m_isRotatingTurn)
                {
                    float prevAngle = m_currentTurnAngle;
                    if (m_smoothTurnRotation) m_currentTurnAngle = Mathf.MoveTowardsAngle(m_currentTurnAngle, m_targetTurnAngle, m_turnRotationSpeed * Time.deltaTime);
                    else m_currentTurnAngle = m_targetTurnAngle;
                    finalRotationDelta = m_currentTurnAngle - prevAngle;
                    if (Mathf.Abs(m_currentTurnAngle - m_targetTurnAngle) < 0.01f) m_isRotatingTurn = false;
                }
            }
            else if (m_currentProperties.m_rotate)
            {
                finalRotationDelta = (m_currentProperties.m_rotateClockwise ? 1 : -1) * m_currentProperties.m_rotationSpeed * Time.deltaTime;
            }

            if (m_currentProperties.m_intermittent)
            {
                m_time += Time.deltaTime;
                if (m_time >= m_currentProperties.m_intervalTime) { m_active = !m_active; m_time = 0; }
            }

            foreach (LaserElement element in elementsList)
            {
                if (finalRotationDelta != 0) element.transform.RotateAround(transform.position, transform.up, finalRotationDelta);

                if (m_active)
                {
                    element.lineRenderer.enabled = true;
                    element.lineRenderer.SetPosition(0, element.transform.position);

                    float maxDist = m_currentProperties.m_maxRadialDistance;
                    Vector3 endPos = element.transform.position + element.transform.forward * maxDist;
                    Vector3 hitPoint = endPos;
                    bool hit = false;
                    Vector3 hitNormal = Vector3.up;

                    // 1. 물리 충돌 체크 (기존 방식 유지)
                    if (m_currentProperties.m_physicsType == LaserProperties.PhysicsType.Physics3D)
                    {
                        if (Physics.Linecast(element.transform.position, endPos, out RaycastHit hit3D, m_currentProperties.m_layerMask))
                        { hit = true; hitPoint = hit3D.point; hitNormal = hit3D.normal; }
                    }
                    else
                    {
                        RaycastHit2D hit2D = Physics2D.Linecast(element.transform.position, endPos, m_currentProperties.m_layerMask);
                        if (hit2D.collider != null) { hit = true; hitPoint = hit2D.point; hitNormal = hit2D.normal; }
                    }

                    element.lineRenderer.SetPosition(1, hitPoint);
                    if (m_assignSparks)
                    {
                        element.sparks.SetActive(hit);
                        if (hit) { element.sparks.transform.position = hitPoint; element.sparks.transform.rotation = Quaternion.LookRotation(hitNormal); }
                    }
                }
                else
                {
                    element.lineRenderer.enabled = false;
                    if (m_assignSparks) element.sparks.SetActive(false);
                }
            }
        }
    }
}