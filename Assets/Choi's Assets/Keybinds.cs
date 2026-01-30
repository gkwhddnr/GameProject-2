using UnityEngine;

public static class Keybinds
{
    const string K_UP = "KB_UP";
    const string K_DOWN = "KB_DOWN";
    const string K_LEFT = "KB_LEFT";
    const string K_RIGHT = "KB_RIGHT";
    const string K_ITEM = "KB_ITEM";
    const string K_INTERACT = "KB_INTERACT";

    public static KeyCode Up { get; private set; }
    public static KeyCode Down { get; private set; }
    public static KeyCode Left { get; private set; }
    public static KeyCode Right { get; private set; }

    public static KeyCode Item { get; private set; }
    public static KeyCode Interact { get; private set; }

    static Keybinds() { Load(); }

    public static void SetDefaults()
    {
        Up = KeyCode.W; Down = KeyCode.S; Left = KeyCode.A; Right = KeyCode.D;
        Item = KeyCode.E; Interact = KeyCode.F;
    }

    public static void Load()
    {
        SetDefaults();
        Up = (KeyCode)PlayerPrefs.GetInt(K_UP, (int)Up);
        Down = (KeyCode)PlayerPrefs.GetInt(K_DOWN, (int)Down);
        Left = (KeyCode)PlayerPrefs.GetInt(K_LEFT, (int)Left);
        Right = (KeyCode)PlayerPrefs.GetInt(K_RIGHT, (int)Right);
        Item = (KeyCode)PlayerPrefs.GetInt(K_ITEM, (int)Item);
        Interact = (KeyCode)PlayerPrefs.GetInt(K_INTERACT, (int)Interact);
    }

    public static void Save()
    {
        PlayerPrefs.SetInt(K_UP, (int)Up);
        PlayerPrefs.SetInt(K_DOWN, (int)Down);
        PlayerPrefs.SetInt(K_LEFT, (int)Left);
        PlayerPrefs.SetInt(K_RIGHT, (int)Right);
        PlayerPrefs.SetInt(K_ITEM, (int)Item);
        PlayerPrefs.SetInt(K_INTERACT, (int)Interact);
        PlayerPrefs.Save();
    }

    public static void RebindUp(KeyCode k) => Up = k;
    public static void RebindDown(KeyCode k) => Down = k;
    public static void RebindLeft(KeyCode k) => Left = k;
    public static void RebindRight(KeyCode k) => Right = k;
    public static void RebindItem(KeyCode k) => Item = k;
    public static void RebindInteract(KeyCode k) => Interact = k;
}
