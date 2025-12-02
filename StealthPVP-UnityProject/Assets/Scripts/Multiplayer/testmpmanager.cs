using UnityEngine;

public class testmpmanager : MonoBehaviour
{
    public Camera cam;
    public GameObject canvas;
    public void OnBtnClick()
    {
        cam.gameObject.SetActive(false);
        canvas.gameObject.SetActive(false);
    }
}
