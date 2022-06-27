using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using Packets;
using Steamworks;
using UnityEngine;

public static class Multiplayer
{
    public delegate void LogMessage(string message);

    public delegate void NewPlayer(CSteamID id);

    public delegate void DisconnectPlayer(CSteamID id);

    public delegate void NetworkMessage(ushort header, BinaryReader reader, CSteamID sender);
    
    private static bool _isServer = false;
    
    private static HSteamListenSocket? _socket;
    private static HSteamNetConnection? _connection;

    private static Dictionary<CSteamID, HSteamNetConnection> _connections = new ();

    public static event LogMessage OnLogMessage;
    public static event NewPlayer OnNewPlayer;
    public static event DisconnectPlayer OnDisconnectPlayer;
    public static event NetworkMessage OnNetworkMessage;
    
    private static IntPtr[] _receiveBuffers = new IntPtr[16];

    public static bool IsServer => _isServer;
    
    public static void StartServer()
    {
        if (_socket != null)
        {
            return;
        }
        
        _connections.Clear();
        _socket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, new SteamNetworkingConfigValue_t[]{});

        Callback<SteamNetConnectionStatusChangedCallback_t>.Create(StatusChangeCallback);
        //Callback<SteamNet

        _isServer = true;
        OnLogMessage?.Invoke("Server created");
        Debug.Log("Server created!");
    }

    public static void Update()
    {
        if (_isServer)
        {
            foreach (var connection in _connections)
            {
                var messages =
                    SteamNetworkingSockets.ReceiveMessagesOnConnection(connection.Value, _receiveBuffers,
                        _receiveBuffers.Length);
                if (messages > 0)
                {
                    HandleMessages(messages, connection.Value);
                }
            }
        }
        else if(_connection.HasValue)
        {
            var messages =
                SteamNetworkingSockets.ReceiveMessagesOnConnection(_connection.Value, _receiveBuffers,
                    _receiveBuffers.Length);
            if (messages > 0)
            {
                HandleMessages(messages, _connection.Value);
            }
        }
    }

    private static void HandleMessages(int messages, HSteamNetConnection connection)
    {
        Debug.Log($"Received {messages} messages");
        SteamNetworkingSockets.GetConnectionInfo(connection, out var info);

        for (var i = 0; i < messages; i++)
        {
            try
            {
                var message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(_receiveBuffers[i]);
                byte[] data = new byte[message.m_cbSize];
                Marshal.Copy(message.m_pData, data, 0, data.Length);

                using var ms = new MemoryStream(data);
                using var br = new BinaryReader(ms);
                var header = br.ReadUInt16();
                
                OnNetworkMessage?.Invoke(header, br, info.m_identityRemote.GetSteamID());
            }
            finally
            {
                Marshal.DestroyStructure<SteamNetworkingMessage_t>(_receiveBuffers[i]);
            }
        }
    }

    private static void StatusChangeCallback(SteamNetConnectionStatusChangedCallback_t param)
    {
        if (param.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
        {
            SteamNetworkingSockets.AcceptConnection(param.m_hConn);
            SteamNetworkingSockets.GetConnectionInfo(param.m_hConn, out var info);
            var name = SteamFriends.GetFriendPersonaName(info.m_identityRemote.GetSteamID());
            
            OnLogMessage?.Invoke("New connection from " + name);
            _connections.Add(info.m_identityRemote.GetSteamID(), param.m_hConn);
            
            OnNewPlayer?.Invoke(info.m_identityRemote.GetSteamID());
        }

        if (param.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer)
        {
            SteamNetworkingSockets.GetConnectionInfo(param.m_hConn, out var info);
            var name = SteamFriends.GetFriendPersonaName(info.m_identityRemote.GetSteamID());
            
            OnLogMessage?.Invoke("Connection to " + name + " closed");
            _connections.Remove(info.m_identityRemote.GetSteamID());
            
            OnDisconnectPlayer?.Invoke(info.m_identityRemote.GetSteamID());
        }
    }

    public static bool ConnectToServer(CSteamID friend)
    {
        _isServer = false;

        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID(friend);

        _connection = SteamNetworkingSockets.ConnectP2P(ref identity, 0, 0, new SteamNetworkingConfigValue_t[] { });
        if (_connection == HSteamNetConnection.Invalid)
        {
            Debug.Log("Connection failed!");
            return false;
        }

        // Ideally we should share a game version / network protocol version here to prevent game version conflicts
        // and only after that spawn and process other incoming packets
        OnLogMessage?.Invoke("Connected");
        return true;
    }

    public static void SendToAll(ushort header, IPacket packet, bool reliable = true)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(header);
        packet.Serialize(bw);
        
        var buffer = ms.GetBuffer();

        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        foreach (var connection in _connections)
        {
            SteamNetworkingSockets.SendMessageToConnection(connection.Value, handle.AddrOfPinnedObject(), (uint) buffer.Length, reliable ? 8 : 0,
                out var messageId);
        }
    }

    public static void SendTo(CSteamID id, ushort header, IPacket packet, bool reliable = true)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(header);
        packet.Serialize(bw);
        
        var buffer = ms.GetBuffer();

        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        if (_connections.ContainsKey(id))
        {
            SteamNetworkingSockets.SendMessageToConnection(_connections[id], handle.AddrOfPinnedObject(), (uint) buffer.Length, reliable ? 8 : 0,
                out var messageId);
        }
    }

    /// <summary>
    /// Does nothing on the server
    /// </summary>
    /// <param name="header"></param>
    /// <param name="packet"></param>
    public static void SendToServer(ushort header, IPacket packet, bool reliable = true)
    {
        if (!_connection.HasValue) return;
        
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(header);
        packet.Serialize(bw);
        
        var buffer = ms.GetBuffer();

        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        SteamNetworkingSockets.SendMessageToConnection(_connection.Value, handle.AddrOfPinnedObject(), (uint) buffer.Length, reliable ? 8 : 0,
            out var messageId);
    }
}