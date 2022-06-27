using System;
using System.Collections;
using System.Collections.Generic;
using Packets;
using Steamworks;
using Unity.Mathematics;
using UnityEngine;

public class NetworkPlayer : MonoBehaviour
{
    public CSteamID id;

    private Vector2 _networkPosition;
    private float _networkRotation;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = Vector3.Lerp(transform.position, _networkPosition, 0.5f);
        transform.rotation = Quaternion.Euler(Vector3.Lerp(transform.rotation.eulerAngles, new Vector3(0, 0, _networkRotation), 0.5f));
    }

    public void Sync(Vector2 position, float rotation)
    {
        // Ideally we would have a common timestamp between client and server to properly know the interpolation time
        // so we would show the world slightly behind so we can properly show or if needed predict the positions
        
        _networkPosition = position;
        _networkRotation = rotation;
        //transform.position = position;
        //transform.rotation = Quaternion.Euler(0, 0, rotation);
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
