using System;
using FabledomMultiplayer.Networking;
using UnityEngine;

namespace FabledomMultiplayer
{
    public class PlayerCursorVisualizer : MonoBehaviour
    {
        public static PlayerCursorVisualizer Instance { get; private set; }

        private GameObject _remoteCursorObj;
        private GameObject _remoteCameraBoundsObj;
        private LineRenderer _boundsLineRenderer;
        private TextMesh _nameTag;

        private Vector3 _targetCursorPos;
        private Vector3 _targetCameraPos;
        private float _targetCameraRotY;
        private float _targetCameraZoom;

        private float _lastSendTime = 0f;
        private const float SEND_INTERVAL = 0.05f; // 20 updates per second

        private void Awake()
        {
            Instance = this;
            CreateRemoteVisuals();
        }

        private void CreateRemoteVisuals()
        {
            // 1. Create Remote 3D Cursor pointer
            _remoteCursorObj = new GameObject("RemotePlayerCursor");
            _remoteCursorObj.transform.SetParent(transform, false);

            // Visual marker: a small vertical gem/crystal cylinder hovering slightly above ground
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "CursorGem";
            marker.transform.SetParent(_remoteCursorObj.transform, false);
            marker.transform.localScale = new Vector3(0.4f, 0.8f, 0.4f);
            marker.transform.localPosition = new Vector3(0, 1.2f, 0);

            // Remove physics collider
            var col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Color the marker gold / cyan
            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.2f, 0.8f, 1f, 0.9f); // Soft cyan
            }

            // Name tag above cursor
            var tagObj = new GameObject("NameTag");
            tagObj.transform.SetParent(_remoteCursorObj.transform, false);
            tagObj.transform.localPosition = new Vector3(0, 2.5f, 0);
            _nameTag = tagObj.AddComponent<TextMesh>();
            _nameTag.text = "Напарник";
            _nameTag.characterSize = 0.25f;
            _nameTag.fontSize = 32;
            _nameTag.color = new Color(1f, 0.9f, 0.5f); // Golden fairy-tale color
            _nameTag.alignment = TextAlignment.Center;
            _nameTag.anchor = TextAnchor.MiddleCenter;

            // 2. Camera Viewport / FOV bounds on the ground
            _remoteCameraBoundsObj = new GameObject("RemoteCameraBounds");
            _remoteCameraBoundsObj.transform.SetParent(transform, false);
            _boundsLineRenderer = _remoteCameraBoundsObj.AddComponent<LineRenderer>();
            _boundsLineRenderer.positionCount = 5;
            _boundsLineRenderer.startWidth = 0.35f;
            _boundsLineRenderer.endWidth = 0.35f;
            _boundsLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _boundsLineRenderer.startColor = new Color(0.2f, 0.8f, 1f, 0.5f);
            _boundsLineRenderer.endColor = new Color(0.2f, 0.8f, 1f, 0.5f);
            _boundsLineRenderer.useWorldSpace = true;

            _remoteCursorObj.SetActive(false);
            _remoteCameraBoundsObj.SetActive(false);
        }

        public void UpdateRemoteData(float curX, float curY, float curZ, float camX, float camZ, float camRotY, float camZoom, int buildingId)
        {
            _targetCursorPos = new Vector3(curX, curY, curZ);
            _targetCameraPos = new Vector3(camX, 0, camZ);
            _targetCameraRotY = camRotY;
            _targetCameraZoom = Mathf.Clamp(camZoom, 5f, 50f);

            if (_remoteCursorObj != null && !_remoteCursorObj.activeSelf)
            {
                _remoteCursorObj.SetActive(true);
                _remoteCameraBoundsObj.SetActive(true);
                _remoteCursorObj.transform.position = _targetCursorPos;
            }
        }

        private void Update()
        {
            // Only active when network is connected and in gameplay
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            {
                if (_remoteCursorObj != null && _remoteCursorObj.activeSelf)
                {
                    _remoteCursorObj.SetActive(false);
                    _remoteCameraBoundsObj.SetActive(false);
                }
                return;
            }

            // 1. Send Local Cursor & Camera to partner
            if (Time.time - _lastSendTime >= SEND_INTERVAL)
            {
                _lastSendTime = Time.time;
                SendLocalState();
            }

            // 2. Smoothly interpolate remote cursor and camera visuals
            if (_remoteCursorObj != null && _remoteCursorObj.activeSelf)
            {
                _remoteCursorObj.transform.position = Vector3.Lerp(_remoteCursorObj.transform.position, _targetCursorPos, Time.deltaTime * 12f);

                // Face name tag towards local camera
                if (Camera.main != null && _nameTag != null)
                {
                    _nameTag.transform.rotation = Camera.main.transform.rotation;
                }

                // Update Camera FOV rectangle on ground
                UpdateCameraBoundsVisual();
            }
        }

        private void SendLocalState()
        {
            Vector3 mouseWorldPos = Vector3.zero;
            if (Camera.main != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    mouseWorldPos = ray.GetPoint(enter);
                }
            }

            Vector3 camPos = Vector3.zero;
            float camRotY = 0f;
            float camZoom = 20f;

            if (GameplayCameraManager.Instance != null && GameplayCameraManager.Instance.controller != null)
            {
                var ctrl = GameplayCameraManager.Instance.controller;
                camPos = ctrl.transform.position;
                camRotY = ctrl.transform.eulerAngles.y;
            }
            else if (Camera.main != null)
            {
                camPos = Camera.main.transform.position;
                camRotY = Camera.main.transform.eulerAngles.y;
            }

            int holdingBuildingId = -1;
            if (PlacementManager.Instance != null && PlacementManager.Instance.active && PlacementManager.Instance.latestPlacementData != null)
            {
                holdingBuildingId = PlacementManager.Instance.latestPlacementData.id;
            }

            byte[] packet = PacketSerializer.SerializeCursor(
                mouseWorldPos.x, mouseWorldPos.y, mouseWorldPos.z,
                camPos.x, camPos.z, camRotY, camZoom,
                holdingBuildingId
            );

            NetworkManager.Instance.SendPacket(packet);
        }

        private void UpdateCameraBoundsVisual()
        {
            if (_boundsLineRenderer == null) return;

            float halfWidth = _targetCameraZoom * 0.8f;
            float halfHeight = _targetCameraZoom * 0.5f;

            Quaternion rot = Quaternion.Euler(0, _targetCameraRotY, 0);

            Vector3 p1 = _targetCameraPos + rot * new Vector3(-halfWidth, 0.2f, -halfHeight);
            Vector3 p2 = _targetCameraPos + rot * new Vector3(halfWidth, 0.2f, -halfHeight);
            Vector3 p3 = _targetCameraPos + rot * new Vector3(halfWidth, 0.2f, halfHeight);
            Vector3 p4 = _targetCameraPos + rot * new Vector3(-halfWidth, 0.2f, halfHeight);

            _boundsLineRenderer.SetPosition(0, p1);
            _boundsLineRenderer.SetPosition(1, p2);
            _boundsLineRenderer.SetPosition(2, p3);
            _boundsLineRenderer.SetPosition(3, p4);
            _boundsLineRenderer.SetPosition(4, p1);
        }
    }
}
