using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    private Rigidbody _rigidbody;
    private float gravityForce = 20.0f;

    // Start is called before the first frame update
    void Start()
    {
        _rigidbody = GetComponent <Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        _rigidbody.mass = transform.localScale.x;
        _rigidbody.AddForce (-transform.up * _rigidbody.mass * gravityForce);
    }
}
