﻿using Cinemachine.Utility;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Cinemachine
{
    /// <summary>
    /// This behaviour is intended to be attached to an empty Transform GameObject, 
    /// and it represents a Virtual Camera within the Unity scene.
    /// 
    /// The Virtual Camera will animate its Transform according to the rules contained
    /// in its CinemachineComponent pipeline (Aim, Body, and Noise).  When the virtual
    /// camera is Live, the Unity camera will assume the position and orientation
    /// of the virtual camera.
    /// 
    /// A virtual camera is not a camera. Instead, it can be thought of as a camera controller,
    /// not unlike a cameraman. It can drive the Unity Camera and control its position, 
    /// orientation, lens settings, and PostProcessing effects. Each Virtual Camera owns 
    /// its own Cinemachine Component Pipeline, through which you provide the instructions 
    /// for dynamically tracking specific game objects. 
    /// 
    /// A virtual camera is very lightweight, and does no rendering of its own. It merely 
    /// tracks interesting GameObjects, and positions itself accordingly. A typical game 
    /// can have dozens of virtual cameras, each set up to follow a particular character 
    /// or capture a particular event. 
    /// 
    /// A Virtual Camera can be in any of three states: 
    /// 
    /// * **Live**: The virtual camera is actively controlling the Unity Camera. The 
    /// virtual camera is tracking its targets and being updated every frame. 
    /// * **Standby**: The virtual camera is tracking its targets and being updated 
    /// every frame, but no Unity Camera is actively being controlled by it. This is 
    /// the state of a virtual camera that is enabled in the scene but perhaps at a 
    /// lower priority than the Live virtual camera. 
    /// * **Disabled**: The virtual camera is present but disabled in the scene. It is 
    /// not actively tracking its targets and so consumes no processing power. However, 
    /// the virtual camera can be made live from the Timeline. 
    /// 
    /// The Unity Camera can be driven by any virtual camera in the scene. The game 
    /// logic can choose the virtual camera to make live by manipulating the virtual 
    /// cameras' enabled flags and their priorities, based on game logic. 
    ///
    /// In order to be driven by a virtual camera, the Unity Camera must have a CinemachineBrain 
    /// behaviour, which will select the most eligible virtual camera based on its priority 
    /// or on other criteria, and will manage blending. 
    /// </summary>
    /// <seealso cref="CinemachineVirtualCameraBase"/>
    /// <seealso cref="LensSettings"/>
    /// <seealso cref="CinemachineComposer"/>
    /// <seealso cref="CinemachineTransposer"/>
    /// <seealso cref="CinemachineBasicMultiChannelPerlin"/>
    [DocumentationSorting(DocumentationSortingAttribute.Level.UserRef)]
    [ExecuteInEditMode, DisallowMultipleComponent]
    [AddComponentMenu("Cinemachine/CinemachineVirtualCamera")]
    public class CinemachineVirtualCamera : CinemachineVirtualCameraBase
    {
        /// <summary>The object that the camera wants to look at (the Aim target).
        /// The Aim component of the CinemachineComponent pipeline
        /// will refer to this target and orient the vcam in accordance with rules and
        /// settings that are provided to it.
        /// If this is null, then the vcam's Transform orientation will be used.</summary>
        [Tooltip("The object that the camera wants to look at (the Aim target).  If this is null, then the vcam's Transform orientation will define the camera's orientation.")]
        [NoSaveDuringPlay]
        public Transform m_LookAt = null;

        /// <summary>The object that the camera wants to move with (the Body target).
        /// The Body component of the CinemachineComponent pipeline
        /// will refer to this target and position the vcam in accordance with rules and
        /// settings that are provided to it.
        /// If this is null, then the vcam's Transform position will be used.</summary>
        [Tooltip("The object that the camera wants to move with (the Body target).  If this is null, then the vcam's Transform position will define the camera's position.")]
        [NoSaveDuringPlay]
        public Transform m_Follow = null;

        /// <summary>Specifies the LensSettings of this Virtual Camera.
        /// These settings will be transferred to the Unity camera when the vcam is live.</summary>
        [FormerlySerializedAs("m_LensAttributes")]
        [Tooltip("Specifies the lens properties of this Virtual Camera.  This generally mirrors the Unity Camera's lens settings, and will be used to drive the Unity camera when the vcam is active.")]
        [LensSettingsProperty]
        public LensSettings m_Lens = LensSettings.Default;

        /// <summary> Collection of parameters that influence how this virtual camera transitions from 
        /// other virtual cameras </summary>
        public TransitionParams m_Transitions;

        /// <summary>Legacy support</summary>
        [SerializeField] [HideInInspector] 
        [FormerlySerializedAs("m_BlendHint")] 
        [FormerlySerializedAs("m_PositionBlending")] private BlendHint m_LegacyBlendHint;

        /// <summary>This is the name of the hidden GameObject that will be created as a child object
        /// of the virtual camera.  This hidden game object acts as a container for the polymorphic
        /// CinemachineComponent pipeline.  The Inspector UI for the Virtual Camera
        /// provides access to this pipleline, as do the CinemachineComponent-family of
        /// public methods in this class.
        /// The lifecycle of the pipeline GameObject is managed automatically.</summary>
        public const string PipelineName = "cm";

        /// <summary>The CameraState object holds all of the information
        /// necessary to position the Unity camera.  It is the output of this class.</summary>
        override public CameraState State { get { return m_State; } }

        /// <summary>Get the LookAt target for the Aim component in the CinemachinePipeline.
        /// If this vcam is a part of a meta-camera collection, then the owner's target
        /// will be used if the local target is null.</summary>
        override public Transform LookAt
        {
            get { return ResolveLookAt(m_LookAt); }
            set { m_LookAt = value; }
        }

        /// <summary>Get the Follow target for the Body component in the CinemachinePipeline.
        /// If this vcam is a part of a meta-camera collection, then the owner's target
        /// will be used if the local target is null.</summary>
        override public Transform Follow
        {
            get { return ResolveFollow(m_Follow); }
            set { m_Follow = value; }
        }

        /// <summary>Internal use only.  Do not call this method.  
        /// Called by CinemachineCore at the appropriate Update time
        /// so the vcam can position itself and track its targets.  This class will
        /// invoke its pipeline and generate a CameraState for this frame.</summary>
        override public void InternalUpdateCameraState(Vector3 worldUp, float deltaTime)
        {
            if (!PreviousStateIsValid)
                deltaTime = -1;

            // Update the state by invoking the component pipeline
            m_State = CalculateNewState(worldUp, deltaTime);
            ApplyPositionBlendMethod(ref m_State, m_Transitions.m_BlendHint);

            // Push the raw position back to the game object's transform, so it
            // moves along with the camera.
            if (!UserIsDragging)
            {
                if (Follow != null)
                    transform.position = State.RawPosition;
                if (LookAt != null)
                    transform.rotation = State.RawOrientation;
            }
            PreviousStateIsValid = true;
        }

        /// <summary>Make sure that the pipeline cache is up-to-date.</summary>
        override protected void OnEnable()
        {
            base.OnEnable();
            m_State = PullStateFromVirtualCamera(Vector3.up, m_Lens);
            InvalidateComponentPipeline();

            // Can't add components during OnValidate
            if (ValidatingStreamVersion < 20170927)
            {
                if (Follow != null && GetCinemachineComponent(CinemachineCore.Stage.Body) == null)
                    AddCinemachineComponent<CinemachineHardLockToTarget>();
                if (LookAt != null && GetCinemachineComponent(CinemachineCore.Stage.Aim) == null)
                    AddCinemachineComponent<CinemachineHardLookAt>();
            }
        }

        /// <summary>Calls the DestroyPipelineDelegate for destroying the hidden
        /// child object, to support undo.</summary>
        protected override void OnDestroy()
        {
            // Make the pipeline visible instead of destroying - this is to keep Undo happy
            foreach (Transform child in transform)
                if (child.GetComponent<CinemachinePipeline>() != null)
                    child.gameObject.hideFlags
                        &= ~(HideFlags.HideInHierarchy | HideFlags.HideInInspector);

            base.OnDestroy();
        }

        /// <summary>Enforce bounds for fields, when changed in inspector.</summary>
        protected override void OnValidate()
        {
            base.OnValidate();
            m_Lens.Validate();
            if (m_LegacyBlendHint != BlendHint.None)
            {
                m_Transitions.m_BlendHint = m_LegacyBlendHint;
                m_LegacyBlendHint = BlendHint.None;
            }
        }

        void OnTransformChildrenChanged()
        {
            InvalidateComponentPipeline();
        }

        void Reset()
        {
            DestroyPipeline();
        }

        /// <summary>
        /// Override component pipeline creation.
        /// This needs to be done by the editor to support Undo.
        /// The override must do exactly the same thing as the CreatePipeline method in this class.
        /// </summary>
        public static CreatePipelineDelegate CreatePipelineOverride;

        /// <summary>
        /// Override component pipeline creation.
        /// This needs to be done by the editor to support Undo.
        /// The override must do exactly the same thing as the CreatePipeline method in
        /// the CinemachineVirtualCamera class.
        /// </summary>
        public delegate Transform CreatePipelineDelegate(
            CinemachineVirtualCamera vcam, string name, CinemachineComponentBase[] copyFrom);

        /// <summary>
        /// Override component pipeline destruction.
        /// This needs to be done by the editor to support Undo.
        /// </summary>
        public static DestroyPipelineDelegate DestroyPipelineOverride;

        /// <summary>
        /// Override component pipeline destruction.
        /// This needs to be done by the editor to support Undo.
        /// </summary>
        public delegate void DestroyPipelineDelegate(GameObject pipeline);

        /// <summary>Destroy any existing pipeline container.</summary>
        private void DestroyPipeline()
        {
            List<Transform> oldPipeline = new List<Transform>();
            foreach (Transform child in transform)
                if (child.GetComponent<CinemachinePipeline>() != null)
                    oldPipeline.Add(child);
            
            foreach (Transform child in oldPipeline)
            {
                if (DestroyPipelineOverride != null)
                    DestroyPipelineOverride(child.gameObject);
                else
                    Destroy(child.gameObject);
            }
            m_ComponentOwner = null;
            PreviousStateIsValid = false;
        }

        /// <summary>Create a default pipeline container.
        /// Note: copyFrom only supported in Editor, not build</summary>
        private Transform CreatePipeline(CinemachineVirtualCamera copyFrom)
        {
            CinemachineComponentBase[] components = null;
            if (copyFrom != null)
            {
                copyFrom.InvalidateComponentPipeline(); // make sure it's up to date
                components = copyFrom.GetComponentPipeline();
            }

            Transform newPipeline = null;
            if (CreatePipelineOverride != null)
                newPipeline = CreatePipelineOverride(this, PipelineName, components);
            else
            {
                GameObject go =  new GameObject(PipelineName);
                go.transform.parent = transform;
                go.AddComponent<CinemachinePipeline>();
                newPipeline = go.transform;
            }
            PreviousStateIsValid = false;
            return newPipeline;
        }

        /// <summary>
        /// Editor API: Call this when changing the pipeline from the editor.
        /// Will force a rebuild of the pipeline cache.
        /// </summary>
        public void InvalidateComponentPipeline() { m_ComponentPipeline = null; }

        /// <summary>Get the hidden CinemachinePipeline child object.</summary>
        public Transform GetComponentOwner() { UpdateComponentPipeline(); return m_ComponentOwner; }

        /// <summary>Get the component pipeline owned by the hidden child pipline container.
        /// For most purposes, it is preferable to use the GetCinemachineComponent method.</summary>
        public CinemachineComponentBase[] GetComponentPipeline() { UpdateComponentPipeline(); return m_ComponentPipeline; }

        /// <summary>Get the component set for a specific stage.</summary>
        /// <param name="stage">The stage for which we want the component</param>
        /// <returns>The Cinemachine component for that stage, or null if not defined</returns>
        public CinemachineComponentBase GetCinemachineComponent(CinemachineCore.Stage stage)
        {
            CinemachineComponentBase[] components = GetComponentPipeline();
            if (components != null)
                foreach (var c in components)
                    if (c.Stage == stage)
                        return c;
            return null;
        }

        /// <summary>Get an existing component of a specific type from the cinemachine pipeline.</summary>
        public T GetCinemachineComponent<T>() where T : CinemachineComponentBase
        {
            CinemachineComponentBase[] components = GetComponentPipeline();
            if (components != null)
                foreach (var c in components)
                    if (c is T)
                        return c as T;
            return null;
        }

        /// <summary>Add a component to the cinemachine pipeline.</summary>
        public T AddCinemachineComponent<T>() where T : CinemachineComponentBase
        {
            // Get the existing components
            Transform owner = GetComponentOwner();
            CinemachineComponentBase[] components = owner.GetComponents<CinemachineComponentBase>();

            T component = owner.gameObject.AddComponent<T>();
            if (component != null && components != null)
            {
                // Remove the existing components at that stage
                CinemachineCore.Stage stage = component.Stage;
                for (int i = components.Length - 1; i >= 0; --i)
                {
                    if (components[i].Stage == stage)
                    {
                        components[i].enabled = false;
                        RuntimeUtility.DestroyObject(components[i]);
                    }
                }
            }
            InvalidateComponentPipeline();
            return component;
        }

        /// <summary>Remove a component from the cinemachine pipeline.</summary>
        public void DestroyCinemachineComponent<T>() where T : CinemachineComponentBase
        {
            CinemachineComponentBase[] components = GetComponentPipeline();
            if (components != null)
            {
                foreach (var c in components)
                {
                    if (c is T)
                    {
                        c.enabled = false;
                        RuntimeUtility.DestroyObject(c);
                        InvalidateComponentPipeline();
                    }
                }
            }
        }

        /// <summary>API for the editor, to make the dragging of position handles behave better.</summary>
        public bool UserIsDragging { get; set; }

        CameraState m_State = CameraState.Default; // Current state this frame

        CinemachineComponentBase[] m_ComponentPipeline = null;
        [SerializeField][HideInInspector] private Transform m_ComponentOwner = null;   // serialized to handle copy/paste
        void UpdateComponentPipeline()
        {
            // Did we just get copy/pasted?
            if (m_ComponentOwner != null && m_ComponentOwner.parent != transform)
            {
                CinemachineVirtualCamera copyFrom = (m_ComponentOwner.parent != null)
                    ? m_ComponentOwner.parent.gameObject.GetComponent<CinemachineVirtualCamera>() : null;
                DestroyPipeline();
                m_ComponentOwner = CreatePipeline(copyFrom);
            }

            // Early out if we're up-to-date
            if (m_ComponentOwner != null && m_ComponentPipeline != null)
                return;

            m_ComponentOwner = null;
            List<CinemachineComponentBase> list = new List<CinemachineComponentBase>();
            foreach (Transform child in transform)
            {
                if (child.GetComponent<CinemachinePipeline>() != null)
                {
                    m_ComponentOwner = child;
                    CinemachineComponentBase[] components = child.GetComponents<CinemachineComponentBase>();
                    foreach (CinemachineComponentBase c in components)
                        if (c.enabled)
                            list.Add(c);
                }
            }

            // Make sure we have a pipeline owner
            if (m_ComponentOwner == null)
                m_ComponentOwner = CreatePipeline(null);

            // Make sure the pipeline stays hidden, even through prefab
            if (CinemachineCore.sShowHiddenObjects)
                m_ComponentOwner.gameObject.hideFlags
                    &= ~(HideFlags.HideInHierarchy | HideFlags.HideInInspector);
            else
                m_ComponentOwner.gameObject.hideFlags
                    |= (HideFlags.HideInHierarchy | HideFlags.HideInInspector);

            // Sort the pipeline
            list.Sort((c1, c2) => (int)c1.Stage - (int)c2.Stage);
            m_ComponentPipeline = list.ToArray();
        }

        private Transform mCachedLookAtTarget;
        private CinemachineVirtualCameraBase mCachedLookAtTargetVcam;
        private CameraState CalculateNewState(Vector3 worldUp, float deltaTime)
        {
            // Initialize the camera state, in case the game object got moved in the editor
            CameraState state = PullStateFromVirtualCamera(worldUp, m_Lens);
            m_Lens = state.Lens;

            Transform lookAtTarget = LookAt;
            if (lookAtTarget != mCachedLookAtTarget)
            {
                mCachedLookAtTarget = lookAtTarget;
                mCachedLookAtTargetVcam = null;
                if (lookAtTarget != null)
                    mCachedLookAtTargetVcam = lookAtTarget.GetComponent<CinemachineVirtualCameraBase>();
            }
            if (lookAtTarget != null)
            {
                if (mCachedLookAtTargetVcam != null)
                    state.ReferenceLookAt = mCachedLookAtTargetVcam.State.FinalPosition;
                else
                    state.ReferenceLookAt = lookAtTarget.position;
            }

            // Update the state by invoking the component pipeline
            CinemachineCore.Stage curStage = CinemachineCore.Stage.Body;
            UpdateComponentPipeline(); // avoid GetComponentPipeline() here because of GC
            bool hasAim = false;
            if (m_ComponentPipeline != null)
            {
                for (int i = 0; i < m_ComponentPipeline.Length; ++i)
                {
                    m_ComponentPipeline[i].PrePipelineMutateCameraState(ref state);
                    if (m_ComponentPipeline[i].Stage == CinemachineCore.Stage.Aim)
                        hasAim = true;
                }
                for (int i = 0; i < m_ComponentPipeline.Length; ++i)
                {
                    curStage = AdvancePipelineStage(
                        ref state, deltaTime, curStage, 
                        (int)m_ComponentPipeline[i].Stage, hasAim);
                    m_ComponentPipeline[i].MutateCameraState(ref state, deltaTime);
                }
            }
            AdvancePipelineStage(
                ref state, deltaTime, curStage, 
                (int)CinemachineCore.Stage.Finalize + 1, hasAim);
            return state;
        }

        private CinemachineCore.Stage AdvancePipelineStage(
            ref CameraState state, float deltaTime,
            CinemachineCore.Stage curStage, int maxStage, bool hasAim)
        {
            while ((int)curStage < maxStage)
            {
                if (!hasAim && curStage == CinemachineCore.Stage.Aim)
                    state.BlendHint |= CameraState.BlendHintValue.IgnoreLookAtTarget;
                InvokePostPipelineStageCallback(this, curStage, ref state, deltaTime);
                ++curStage;
            }
            return curStage;
        }

        // This is a hack for FreeLook rigs - to be removed
        internal void SetStateRawPosition(Vector3 pos) { m_State.RawPosition = pos; }

        /// <summary>This is called to notify the vcam that a target got warped,
        /// so that the vcam can update its internal state to make the camera 
        /// also warp seamlessy.</summary>
        /// <param name="target">The object that was warped</param>
        /// <param name="positionDelta">The amount the target's position changed</param>
        public override void OnTargetObjectWarped(Transform target, Vector3 positionDelta)
        {
            if (target == Follow)
            {
                transform.position += positionDelta;
                m_State.RawPosition += positionDelta;
            }
            UpdateComponentPipeline(); // avoid GetComponentPipeline() here because of GC
            if (m_ComponentPipeline != null)
            {
                for (int i = 0; i < m_ComponentPipeline.Length; ++i)
                    m_ComponentPipeline[i].OnTargetObjectWarped(target, positionDelta);
            }
            base.OnTargetObjectWarped(target, positionDelta);
        }

        /// <summary>If we are transitioning from another vcam, grab the position from it.</summary>
        /// <param name="fromCam">The camera being deactivated.  May be null.</param>
        /// <param name="worldUp">Default world Up, set by the CinemachineBrain</param>
        /// <param name="deltaTime">Delta time for time-based effects (ignore if less than or equal to 0)</param>
        public override void OnTransitionFromCamera(
            ICinemachineCamera fromCam, Vector3 worldUp, float deltaTime) 
        {
            base.OnTransitionFromCamera(fromCam, worldUp, deltaTime);
            if (m_Transitions.m_InheritPosition)
            {
                var brain = CinemachineCore.Instance.FindPotentialTargetBrain(this);
                if (brain != null)
                {
                    transform.position = brain.transform.position;
                    transform.rotation = brain.transform.rotation;
                    PreviousStateIsValid = false;
                    InternalUpdateCameraState(worldUp, deltaTime);
                }
            }
            if (m_Transitions.m_OnCameraLive != null)
                m_Transitions.m_OnCameraLive.Invoke(this);
        }
    }
}
