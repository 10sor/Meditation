using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StoreBanner))]
public class StoreBannerEditor : Editor
{
    private void OnSceneGUI()
    {
        StoreBanner banner = (StoreBanner)target;

        if (banner == null) return;

        Handles.BeginGUI();

        GUILayout.BeginArea(new Rect(15, 15, 720, 220));

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold
        };

        if (GUILayout.Button(" Get Full Version on Asset Store", buttonStyle, GUILayout.Height(50)))
        {
            banner.OpenStorePage();
        }

        GUILayout.EndArea();

        Handles.EndGUI();
    }
}