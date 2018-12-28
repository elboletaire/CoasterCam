using System;
using System.Collections.Generic;
using Parkitect.UI;
using UnityEngine;

namespace CoasterCam
{
    internal class CoasterCam : MonoBehaviour
    {
        public static CoasterCam Instance;

        private readonly List<Transform> _seats = new List<Transform>();

        private Camera _cam;

        private Coaster _coaster;
        private GameObject _coasterCam;

        private GameObject _currentSeat;

        private float _fps;

        private bool _isOnRide;
        private readonly float _maximumX = 60F;
        private readonly float _maximumY = 40F;


        private readonly float _minimumX = -60F;
        private readonly float _minimumY = -40F;
        private GameObject _origCam;
        private int _origQualityLevel;
        private int _origResoHeight;
        private int _origResoWidth;

        private float _origShadowDist;
        private Transform _parent;
        private Transform _head;

        private int _seatIndex;


        private List<Tuple<string, Vector3, Vector3>> _views = new List<Tuple<string, Vector3, Vector3>>()
        {
            new Tuple<string, Vector3, Vector3>("First person", Vector3.zero, Vector3.zero),
            new Tuple<string, Vector3, Vector3>("Third person", new Vector3(0, .39f, -1.35f), new Vector3(20,0,0)),
            new Tuple<string, Vector3, Vector3>("Front person", new Vector3(0, -.15f, 1.35f), new Vector3(0,180,0)),
        };

        private int _currentView;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private KeyCode GetEnterKeyCode()
        {
            return Settings.Instance.getKeyMapping(gameObject.name + "/enter");
        }

        private void Update()
        {


            if (Input.GetKeyUp(GetEnterKeyCode()) && !_isOnRide && !UIUtility.isInputFieldFocused())
            {
                var ride = Utility.getObjectBelowMouse().hitObject;

                var attr = ride.gameObject.GetComponentInChildren<Attraction>();

                if (attr == null) attr = ride.gameObject.GetComponentInParent<Attraction>();

                _coaster = attr as Coaster;


                if (attr != null)
                {
                    _seats.Clear();
                    _seatIndex = 0;

                    Utility.recursiveFindTransformsStartingWith("seat", attr.transform, _seats);



                    if (_seats.Count > 0)
                        EnterCoasterCam(_seats[_seatIndex].gameObject);
                }
            }
            else if (Input.GetKeyUp(GetEnterKeyCode()) || Input.GetKeyDown(KeyCode.Escape))
            {
                LeaveCoasterCam();
            }


            if ((Input.GetKeyUp(KeyCode.A) || Input.GetKeyUp(KeyCode.D)) && _isOnRide)
            {
                LeaveCoasterCam();

                if (Input.GetKeyUp(KeyCode.D) )
                    if (++_currentView == _views.Count)
                        _currentView = 0;

                if (Input.GetKeyUp(KeyCode.A))
                    if (--_currentView < 0)
                        _currentView = _views.Count - 1;

                EnterCoasterCam(_seats[_seatIndex].gameObject);
            }

            if (_isOnRide)
            {
                if (Math.Abs(Input.GetAxis("Mouse ScrollWheel")) > 0.1)
                {
                    LeaveCoasterCam();

                    if (Input.GetAxis("Mouse ScrollWheel") > 0)
                        if (++_seatIndex == _seats.Count)
                            _seatIndex = 0;

                    if (Input.GetAxis("Mouse ScrollWheel") < 0)
                        if (--_seatIndex < 0)
                            _seatIndex = _seats.Count - 1;

                    EnterCoasterCam(_seats[_seatIndex].gameObject);
                }

                AdaptFarClipPaneToFps();
            }
        }

        private void FixedUpdate()
        {
            if (Input.GetMouseButton(1))
            {
                return;
            }
            if (_isOnRide && _seatIndex < 2)
            {
                //_head.transform.position = _currentSeat.transform.position + _currentSeat.transform.up * 0.35f +
                //                                 _currentSeat.transform.forward * 0.1f;
                if (_coaster != null)
                {
                    var point1 = _coaster.Track.getPoint(_coaster.Track.trains[0].currentTrackPosition);
                    var lookAtPosition = _coaster.Track.trains[0].currentTrackPosition + 1.5f + _coaster.Track.trains[0].velocity / 30;
                    var point2 = _coaster.Track.getPoint(lookAtPosition);
                    var oldRot = _head.transform.localEulerAngles;


                    var xDir = Vector3.ProjectOnPlane(point2, _parent.up) - Vector3.ProjectOnPlane(point1, _parent.up);
                    var rotationX = -Vector3.SignedAngle(xDir, _parent.forward, _parent.up) / 2f;
                    if (Mathf.Abs(rotationX) > _maximumX + 10)
                        rotationX = 0;
                    rotationX = Mathf.Clamp(rotationX, _minimumX, _maximumX);

                    rotationX = Mathf.LerpAngle(oldRot.y, rotationX, Time.deltaTime * 2f + Time.deltaTime * _coaster.Track.trains[0].velocity / 20);

                    var yDir = Vector3.ProjectOnPlane(point2, _parent.right) -
                               Vector3.ProjectOnPlane(point1, _parent.right);
                    var rotationY = -Vector3.SignedAngle(yDir, _parent.forward, _parent.right) / 2f;
                    if (Mathf.Abs(rotationY) > _maximumY + 10)
                        rotationY = 0;
                    rotationY = Mathf.Clamp(rotationY, _minimumY, _maximumY);
                    rotationY = Mathf.LerpAngle(oldRot.x, rotationY, Time.deltaTime * 2f + Time.deltaTime * _coaster.Track.trains[0].velocity / 20);

                    var rot = new Vector3(rotationY, rotationX, 0);
                    _head.transform.localEulerAngles = rot;
                }
            }
        }


        private void AdaptFarClipPaneToFps()
        {
            _fps = 1.0f / Time.deltaTime;

            if (_fps < 50) _cam.farClipPlane = Math.Max(30, _cam.farClipPlane - 0.3f);

            if (_fps > 55) _cam.farClipPlane = Math.Min(120, _cam.farClipPlane + 0.2f);
        }



        public void EnterCoasterCam(GameObject onGo)
        {
            if (_isOnRide)
                return;

            UIWorldOverlayController.Instance.gameObject.SetActive(false);

            var tag = Camera.main.tag;

            _origCam = Camera.main.gameObject;


            _coasterCam = Instantiate(_origCam);

            _origCam.SetActive(false);

            _coasterCam.tag = tag;
            DestroyImmediate(_coasterCam.GetComponent<CameraController>());

            var go = new GameObject();
            var cam2 = go.AddComponent<Camera>();

            var cam = _coasterCam.GetComponent<Camera>();

            /**
            * Ugliest code of all time, but hey, it works!
            */
            cam.aspect = cam2.aspect;
            cam.backgroundColor = cam2.backgroundColor;
            cam.clearFlags = cam2.clearFlags;
            cam.clearStencilAfterLightingPass = cam2.clearStencilAfterLightingPass;
            //cam.cullingMask = cam2.cullingMask;
            cam.depth = cam2.depth;
            cam.depthTextureMode = cam2.depthTextureMode;
            //cam.farClipPlane = cam2.farClipPlane;
            cam.fieldOfView = cam2.fieldOfView;
            cam.allowHDR = cam2.allowHDR;
            cam.layerCullDistances = cam2.layerCullDistances;
            cam.layerCullSpherical = cam2.layerCullSpherical;
            cam.pixelRect = cam2.pixelRect;
            cam.projectionMatrix = cam2.projectionMatrix;
            cam.rect = cam2.rect;
            cam.renderingPath = cam2.renderingPath;
            cam.stereoConvergence = cam2.stereoConvergence;
            cam.stereoSeparation = cam2.stereoSeparation;
            cam.targetDisplay = cam2.targetDisplay;
            cam.targetTexture = cam2.targetTexture;
            cam.transparencySortMode = cam2.transparencySortMode;
            cam.useOcclusionCulling = cam2.useOcclusionCulling;


            DestroyImmediate(go);

            CullingGroupManager.Instance.setTargetCamera(_coasterCam.GetComponent<Camera>());
            _coasterCam.GetComponent<Camera>().nearClipPlane = 0.05f;
            _coasterCam.GetComponent<Camera>().farClipPlane = 100f;
            _coasterCam.GetComponent<Camera>().depthTextureMode = DepthTextureMode.DepthNormals;

            _coasterCam.AddComponent<AudioListener>();

            _currentSeat = onGo;

            _parent = new GameObject().transform;
            _parent.parent = onGo.transform;
            _parent.localPosition = new Vector3(0, 0.35f, 0.1f);
            _parent.localRotation = Quaternion.identity;

            _head = new GameObject().transform;
            _head.parent = _parent;
            _head.localPosition = Vector3.zero;
            _head.localRotation = Quaternion.identity;

            _coasterCam.transform.parent = _head;
            _coasterCam.transform.localPosition = _views[_currentView].Item2;
            _coasterCam.transform.localEulerAngles = _views[_currentView].Item3;


            _head.gameObject.AddComponent<MouseLookAround>();

            _cam = _coasterCam.GetComponent<Camera>();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _isOnRide = true;
        }

        public void LeaveCoasterCam()
        {
            if (!_isOnRide)
                return;
            
            Destroy(_head.gameObject);
            _origCam.SetActive(true);
            CullingGroupManager.Instance.setTargetCamera(_origCam.GetComponent<Camera>());
            
            GameController.Instance.cameraController = _origCam.GetComponent<CameraController>();

            UIWorldOverlayController.Instance.gameObject.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _isOnRide = false;

            
        }
    }
}