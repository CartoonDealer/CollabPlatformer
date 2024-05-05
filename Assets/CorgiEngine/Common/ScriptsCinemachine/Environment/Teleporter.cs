﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace MoreMountains.CorgiEngine
{	
	[AddComponentMenu("Corgi Engine/Environment/Teleporter")]
	/// <summary>
	/// Add this script to a trigger collider2D to teleport objects from that object to its destination
	/// </summary>
	public class Teleporter : ButtonActivated 
	{
        /// the possible modes the teleporter can interact with the camera system on activation, either doing nothing, teleporting the camera to a new position, or blending between Cinemachine virtual cameras
        public enum CameraModes { DoNothing, TeleportCamera, CinemachinePriority }
        /// the possible teleportation modes (either 1-frame instant teleportation, or tween between this teleporter and its destination)
        public enum TeleportationModes { Instant, Tween }

        [Header("Teleporter")]
        /// if true, this won't teleport non player characters
        public bool OnlyAffectsPlayer = true;
        /// the offset to apply when exiting this teleporter
        public Vector3 ExitOffset;
        /// the selected teleportation mode 
        public TeleportationModes TeleportationMode = TeleportationModes.Instant;
        /// the curve to apply to the teleportation tween 
        [MMEnumCondition("TeleportationMode", (int)TeleportationModes.Tween)]
        public MMTween.MMTweenCurve TweenCurve = MMTween.MMTweenCurve.EaseInCubic;
        /// whether or not to maintain the x value of the teleported object on exit
        public bool MaintainXEntryPositionOnExit = false;
        /// whether or not to maintain the y value of the teleported object on exit
        public bool MaintainYEntryPositionOnExit = false;

        [Header("Destination")]
		/// the teleporter's destination
		public Teleporter Destination;        
        /// if this is true, the teleported object will be put on the destination's ignore list, to prevent immediate re-entry. If your 
        /// destination's offset is far enough from its center, you can set that to false
        public bool AddToDestinationIgnoreList = true;

        [Header("Rooms")]
        /// the chosen camera mode
        public CameraModes CameraMode = CameraModes.TeleportCamera;
        /// the room this teleporter belongs to
        public Room CurrentRoom;
        /// the target room
        public Room TargetRoom;
        
        [Header("MMFader Transition")]
		/// if this is true, a fade to black will occur when teleporting
		public bool TriggerFade = false;
        /// the ID of the fader to target
        [MMCondition("TriggerFade", true)]
        public int FaderID = 0;
        /// the curve to use to fade to black
        [MMCondition("TriggerFade", true)]
        public MMTweenType FadeTween = new MMTweenType(MMTween.MMTweenCurve.EaseInCubic);

        [Header("Mask")]
        /// whether or not we should ask to move a MMSpriteMask on activation
        public bool MoveMask = true;
        /// the curve to move the mask along to
        public MMTween.MMTweenCurve MoveMaskCurve = MMTween.MMTweenCurve.EaseInCubic;
        /// the method to move the mask
        public MMSpriteMaskEvent.MMSpriteMaskEventTypes MoveMaskMethod = MMSpriteMaskEvent.MMSpriteMaskEventTypes.ExpandAndMoveToNewPosition;
        /// the duration of the mask movement (usually the same as the DelayBetweenFades
        public float MoveMaskDuration = 0.2f;

        [Header("Freeze")]
        /// whether or not time should be frozen during the transition
        public bool FreezeTime = false;
        /// whether or not the character should be frozen (input blocked) for the duration of the transition
        public bool FreezeCharacter = true;

        [Header("Teleport Sequence")]
        /// the delay (in seconds) to apply before running the sequence
        public float InitialDelay = 0.1f;
        /// the duration (in seconds) after the initial delay covering for the fade out of the scene
        public float FadeOutDuration = 0.2f;
        /// the duration (in seconds) to wait for after the fade out and before the fade in
        public float DelayBetweenFades = 0.3f;
        /// the duration (in seconds) after the initial delay covering for the fade in of the scene
        public float FadeInDuration = 0.2f;
        /// the duration (in seconds) to apply after the fade in of the scene
        public float FinalDelay = 0.1f;

        protected Character _player;
	    protected List<Transform> _ignoreList;

        protected WaitForSecondsRealtime _initialDelayWaitForSeconds;
        protected WaitForSecondsRealtime _fadeOutDurationWaitForSeconds;
        protected WaitForSecondsRealtime _halfDelayBetweenFadesWaitForSeconds;
        protected WaitForSecondsRealtime _fadeInDurationWaitForSeconds;
        protected WaitForSecondsRealtime _finalDelayyWaitForSeconds;
        protected Vector3 _entryPosition;
        protected Vector3 _newPosition;

        /// <summary>
        /// On start we initialize our ignore list
        /// </summary>
        protected virtual void Awake()
		{
            InitializeTeleporter();
        }

        /// <summary>
        /// Grabs the current room in the parent if needed
        /// </summary>
        protected virtual void InitializeTeleporter()
        {
            _ignoreList = new List<Transform>();
            if (CurrentRoom == null)
            {
                CurrentRoom = this.gameObject.GetComponentInParent<Room>();
            }
            _initialDelayWaitForSeconds = new WaitForSecondsRealtime(InitialDelay);
            _fadeOutDurationWaitForSeconds = new WaitForSecondsRealtime(FadeOutDuration);
            _halfDelayBetweenFadesWaitForSeconds = new WaitForSecondsRealtime(DelayBetweenFades/2f);
            _fadeInDurationWaitForSeconds = new WaitForSecondsRealtime(FadeInDuration);
            _finalDelayyWaitForSeconds = new WaitForSecondsRealtime(FinalDelay);
        }

	    /// <summary>
	    /// Triggered when something enters the teleporter
	    /// </summary>
	    /// <param name="collider">Collider.</param>
	    protected override void OnTriggerEnter2D(Collider2D collider)
        {
            // if the object that collides with the teleporter is on its ignore list, we do nothing and exit.
            if (_ignoreList.Contains(collider.transform))
			{
				return;
			}			

			if (collider.GetComponent<Character>()!=null)
			{
				_player = collider.GetComponent<Character>();
			}
            
			// if the teleporter is supposed to only affect the player (well, corgiControllers), we do nothing and exit
			if (OnlyAffectsPlayer || !AutoActivation)
            {
                base.OnTriggerEnter2D(collider);
			}
			else
            {
                Teleport(collider);
			}
		}

		/// <summary>
		/// If we're button activated and if the button is pressed, we teleport
		/// </summary>
		public override void TriggerButtonAction()
		{
			if (!CheckNumberOfUses())
			{
				return;
			}
			if (_player.GetComponent<Collider2D>()!=null)
			{
				base.TriggerButtonAction ();
				Teleport(_player.GetComponent<Collider2D>());
			}
		}

		/// <summary>
		/// Teleports whatever enters the portal to a new destination
		/// </summary>
		protected virtual void Teleport(Collider2D collider)
		{
            _entryPosition = collider.transform.position;
            // if the teleporter has a destination, we move the colliding object to that destination
            if (Destination != null)
            {
                StartCoroutine(TeleportSequence(collider));         
			}
		}
        
        /// <summary>
        /// Handles the teleport sequence (fade in, pause, fade out)
        /// </summary>
        /// <param name="collider"></param>
        /// <returns></returns>
        protected virtual IEnumerator TeleportSequence(Collider2D collider)
		{
            SequenceStart(collider);

            yield return _initialDelayWaitForSeconds;

            AfterInitialDelay(collider);

            yield return _fadeOutDurationWaitForSeconds;

            AfterFadeOut(collider);
            
            yield return _halfDelayBetweenFadesWaitForSeconds;

            BetweenFades(collider);

            yield return _halfDelayBetweenFadesWaitForSeconds;

            AfterDelayBetweenFades(collider);

            yield return _fadeInDurationWaitForSeconds;

            AfterFadeIn(collider);

            yield return _finalDelayyWaitForSeconds;

            SequenceEnd(collider);
        }

        /// <summary>
        /// Describes the events happening before the initial fade in
        /// </summary>
        /// <param name="collider"></param>
        protected virtual void SequenceStart(Collider2D collider)
        {
            ActivateZone();

            if (CameraMode == CameraModes.TeleportCamera)
            {
                MMCameraEvent.Trigger(MMCameraEventTypes.StopFollowing);
            }

            if (FreezeTime)
            {
                MMTimeScaleEvent.Trigger(MMTimeScaleMethods.For, 0f, 0f, false, 0f, true);
            }

            if (FreezeCharacter && (_player != null))
            {
                _player.Freeze();
            }
        }

        /// <summary>
        /// Describes the events happening after the initial delay has passed
        /// </summary>
        /// <param name="collider"></param>
        protected virtual void AfterInitialDelay(Collider2D collider)
        {            
            if (TriggerFade)
            {
                MMFadeInEvent.Trigger(FadeOutDuration, FadeTween, FaderID, false, LevelManager.Instance.Players[0].transform.position);
            }
        }

        /// <summary>
        /// Describes the events happening once the initial fade in is complete
        /// </summary>
        protected virtual void AfterFadeOut(Collider2D collider)
        {   
            if (AddToDestinationIgnoreList)
            {
                Destination.AddToIgnoreList(collider.transform);
            }            
            
            if (CameraMode == CameraModes.CinemachinePriority)
            {
                MMCameraEvent.Trigger(MMCameraEventTypes.ResetPriorities);
                MMCinemachineBrainEvent.Trigger(MMCinemachineBrainEventTypes.ChangeBlendDuration, DelayBetweenFades);
            }

            if (CurrentRoom != null)
            {
                CurrentRoom.PlayerExitsRoom();
            }
            
            if (TargetRoom != null)
            {
                TargetRoom.PlayerEntersRoom();
                TargetRoom.VirtualCamera.Priority = 10;
                MMSpriteMaskEvent.Trigger(MoveMaskMethod, (Vector2)TargetRoom.RoomCollider.bounds.center + TargetRoom.RoomCollider.offset, TargetRoom.RoomCollider.bounds.size, MoveMaskDuration, MoveMaskCurve);
            }
        }

        /// <summary>
        /// Teleports the object going through the teleporter, either instantly or by tween
        /// </summary>
        /// <param name="collider"></param>
        protected virtual void TeleportCollider(Collider2D collider)
        {
            _newPosition = Destination.transform.position + Destination.ExitOffset;
            if (MaintainXEntryPositionOnExit)
            {
                _newPosition.x = _entryPosition.x;
            }
            if (MaintainYEntryPositionOnExit)
            {
                _newPosition.y = _entryPosition.y;
            }
            _newPosition.z = collider.transform.position.z;

            switch (TeleportationMode)
            {
                case TeleportationModes.Instant:
                    collider.transform.position = _newPosition;
                    _ignoreList.Remove(collider.transform);
                    break;
                case TeleportationModes.Tween:
                    StartCoroutine(TeleportTweenCo(collider, collider.transform.position, _newPosition));
                    break;
            }
        }

        /// <summary>
        /// Tweens the object from origin to destination
        /// </summary>
        /// <param name="collider"></param>
        /// <param name="origin"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        protected virtual IEnumerator TeleportTweenCo(Collider2D collider, Vector3 origin, Vector3 destination)
        {
            float startedAt = Time.unscaledTime;
            while (Time.unscaledTime - startedAt < DelayBetweenFades/2f)
            {
                float elapsedTime = Time.unscaledTime - startedAt;
                collider.transform.position = MMTween.Tween(elapsedTime, 0f, DelayBetweenFades/2f, origin, destination, TweenCurve);
                yield return null;
            }
            _ignoreList.Remove(collider.transform);
        }

        /// <summary>
        /// Describes what happens midway through the fade
        /// </summary>
        /// <param name="collider"></param>
        protected virtual void BetweenFades(Collider2D collider)
        {
            TeleportCollider(collider);
        }

        /// <summary>
        /// Describes the events happening after the pause between the fade in and the fade out
        /// </summary>
        protected virtual void AfterDelayBetweenFades(Collider2D collider)
        {

            if (CameraMode == CameraModes.TeleportCamera)
            {
                if (LevelManager.Instance.LevelCameraController != null)
                {
                    LevelManager.Instance.LevelCameraController.TeleportCameraToTarget();
                }                
                MMCameraEvent.Trigger(MMCameraEventTypes.StartFollowing);
            }

            if (TriggerFade)
            {
                MMFadeOutEvent.Trigger(FadeInDuration, FadeTween, FaderID, false, LevelManager.Instance.Players[0].transform.position);
            }
        }

        /// <summary>
        /// Describes the events happening after the fade in of the scene
        /// </summary>
        /// <param name="collider"></param>
        protected virtual void AfterFadeIn(Collider2D collider)
        {

        }

        /// <summary>
        /// Describes the events happening after the fade out is complete, so at the end of the teleport sequence
        /// </summary>
        protected virtual void SequenceEnd(Collider2D collider)
        {
            if (FreezeCharacter && (_player != null))
            {
                _player.UnFreeze();
            }

            if (FreezeTime)
            {
                MMTimeScaleEvent.Trigger(MMTimeScaleMethods.Unfreeze, 1f, 0f, false, 0f, false);
            }
        }

	    /// <summary>
	    /// When something exits the teleporter, if it's on the ignore list, we remove it from it, so it'll be considered next time it enters.
	    /// </summary>
	    /// <param name="collider">Collider.</param>
        public override void TriggerExitAction(GameObject collider)
        {
            if (_ignoreList.Contains(collider.transform))
            {
                _ignoreList.Remove(collider.transform);
            }
            base.TriggerExitAction(collider);
        }

        /// <summary>
        /// Adds an object to the ignore list, which will prevent that object to be moved by the teleporter while it's in that list
        /// </summary>
        /// <param name="objectToIgnore">Object to ignore.</param>
        public virtual void AddToIgnoreList(Transform objectToIgnore)
		{
			_ignoreList.Add(objectToIgnore);
        }
        
        /// <summary>
        /// On draw gizmos, we draw arrows to the target destination and target room if there are any
        /// </summary>
        protected virtual void OnDrawGizmos()
        {
            if (Destination != null)
            {
                // draws an arrow from this teleporter to its destination
                MMDebug.DrawGizmoArrow(this.transform.position, (Destination.transform.position + Destination.ExitOffset) - this.transform.position, Color.cyan, 1f, 25f);
                // draws a point at the exit position 
                MMDebug.DebugDrawCross(this.transform.position + ExitOffset, 0.5f, Color.yellow);
                MMDebug.DrawPoint(this.transform.position + ExitOffset, Color.yellow, 0.5f);
            }

            if (TargetRoom != null)
            {
                // draws an arrow to the destination room
                MMDebug.DrawGizmoArrow(this.transform.position, TargetRoom.transform.position - this.transform.position, MMColors.Pink, 1f, 25f);
            }
        }
	}
}