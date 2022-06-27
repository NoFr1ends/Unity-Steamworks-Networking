using System;
using System.Collections;
using System.Collections.Generic;
using Packets;
using UnityEngine;

public class LocalPlayer : MonoBehaviour
{
    private Vector2 _lastNetworkPosition;
    private float _lastNetworkRotation;

    private float _lastPacket = 0;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        var position = Input.mousePosition;
        position.z = 0;

        var objectPos = Camera.main.WorldToScreenPoint(transform.position);
        position -= objectPos;

        var angle = Mathf.Atan2(position.y, position.x) * Mathf.Rad2Deg - 90;
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));

        if (Input.GetKey(KeyCode.Space))
        {
            transform.position += transform.up * 2.0f * Time.deltaTime;
        }

        if ((_lastNetworkPosition != (Vector2) transform.position || Math.Abs(angle - _lastNetworkRotation) > 2.0f) && Time.time - _lastPacket > 0.1f)
        {
            Multiplayer.SendToServer(0x03, GetState(), false);

            _lastNetworkPosition = transform.position;
            _lastNetworkRotation = angle;
            _lastPacket = Time.time;
        }
    }

    public PlayerUpdate GetState()
    {
        return new PlayerUpdate
        {
            Position = transform.position,
            Rotation = transform.rotation.eulerAngles.z
        };
    }
}
