using UnityEngine;

public class testmpmanager : MonoBehaviour
{
    public Camera cam;
    public Canvas canvas;
    public void OnBtnClick()
    {
        cam.gameObject.SetActive(false);
        canvas.gameObject.SetActive(false);
    }
}
