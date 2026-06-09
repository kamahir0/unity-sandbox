using UnityEditor;
using UnityEngine.UI;
using UxmlToUgui;

public class ButtonEx : Button
{
    [InitializeOnLoadMethod]
    public static void Register()
    {
        UxmlToUguiRegistry.OverrideComponent<Button, ButtonEx>();
    }
}
