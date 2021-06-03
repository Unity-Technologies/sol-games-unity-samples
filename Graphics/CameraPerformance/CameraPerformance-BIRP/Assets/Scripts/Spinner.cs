using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class Spinner : MonoBehaviour
{
    [SerializeField]
    float m_DegreesPerSecond;
    Vector3 m_RotationAxis;

    void Awake()
    {
        m_RotationAxis = Random.insideUnitSphere.normalized;
    }

    void Update()
    {
        transform.Rotate(m_RotationAxis, m_DegreesPerSecond * Time.deltaTime);
    }
}
