using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // Slot untuk Karakter Utama
    public Vector3 offset = new Vector3(0f, 0f, -10f); // Jarak kamera
    public float smoothSpeed = 5f; // Kecepatan ngikutinnya

    void LateUpdate()
    {
        if (target != null)
        {
            Vector3 posisiTujuan = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, posisiTujuan, smoothSpeed * Time.deltaTime);
        }
    }
}