using UnityEngine;

public class StoreBanner : MonoBehaviour
{
    public string storeUrl = "https://assetstore.unity.com/packages/3d/environments/landscapes/customizable-rocks-and-cliffs-pbr-303801";

    public void OpenStorePage()
    {
        if (!string.IsNullOrEmpty(storeUrl))
        {
            Application.OpenURL(storeUrl);
        }
    }
}