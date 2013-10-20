﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Quantum.States;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Common;
using FVector2 = Microsoft.Xna.Framework.FVector2;

public class Player : MonoBehaviour {
	/* All possible directions the player can be in. */
	public enum Direction {
		LEFT,
		RIGHT,
		TURNING_LEFT_TO_RIGHT,
		TURNING_RIGHT_TO_LEFT,
		NONE,
	};

	/* Current game state that the player is in. */
	public GameState currentState;
	public string currentStateString; // Used for debugging.

	/* Previous game state that the player was in. */
	public GameState previousState;
	public string previousStateString; // Used for debugging.
	
	
	/* Current direction that the player is in. */
	public Direction currentDirection;
	
	/* Last direction that the player was in. */
	public Direction previousDirection;
	
	
	/* The animation manager of the player. */
	public tk2dSpriteAnimator animator;
	
	
	/* The walking speed of the player. */
	public float walkingVelocity = 10.0f;
	
	/* The jumping speed of the player. */
	public float jumpingVelocity = 2.5f;
	
	public float maxVelocityX = 12.0f;
	public float maxVelocityY = 18.0f;
	
	public float jumpReleaseVelocityFalloffRate = 0.2f;
	
	/* The current pickup that the player is carrying. */
	public Pickup carriedPickup;
	
	
	/* There's a cooldown time for both picking up and setting down pickups. */
	public float pickupCooldown = 0.25f;
	private float pickupCooldownTimeRemaining = 0.0f;
	
	/* There's a cooldown time for entering portals. */
	public float vortexCooldown = 0.25f;
	private float vortexCooldownTimeRemaining = 0.0f;
	
	
	
	public Body body;
	private Body childBody;
	
	
	
	/* The state the action button is currently in. */
	public enum ActionButtonStates {
		NONE,
		CAN_DROP,
		CAN_PICKUP,
		CAN_ACTIVATE_VORTEX,
	};
	
	public ActionButtonStates currentActionButtonState;
	
	/* Am I in range of activating a vortex? */
	private bool nearVortex;
	
	/* Am I allowed to move right now? */
	public bool canMove;
	
	
	
	
	void Start () {
		gameObject.tag = "Player";
		
		//currentDirection = Direction.RIGHT;
		previousDirection = Direction.RIGHT;
		
		currentState = new ProfessorStandingState(this);
		currentStateString = currentState.ToString();
		previousState = new ProfessorStandingState(this);
		previousStateString = previousState.ToString();
		
		body = GetComponent<FSBodyComponent>().PhysicsBody;
		body.FixedRotation = true;
		body.FixtureList[0].UserData = "Player";
		body.FixtureList[0].UserTag = "Player";
		
		body.OnCollision += OnCollisionEvent;
		body.OnSeparation += OnCollisionSeparation;
	
		animator = GetComponent<tk2dSpriteAnimator>();
		if (!animator) {
			throw new Exception("No tk2dSpriteAnimator was attached to the Player!!!");	
		}
		animator.AnimationCompleted = HandleAnimationCompleted;
		
		nearVortex = false;
		canMove = true;
	}
	
	
	
	
	
	void Update () {
		/* Update direction. */
		previousDirection = currentDirection;
		currentDirection = NextDirection();
		
		/* Update game state. */
		previousState = currentState;
		currentStateString = currentState.ToString();
		currentState = currentState.NextState();
		previousStateString = previousState.ToString();
		
		/* Let the current game state do what it needs to do. */
		currentState.Logic();
		HandleExtraLogic();
		HandleVelocityCap();
		
		//Debug.Log (body.LinearVelocity);
	}
	
	
	
	
	
	bool OnCollisionEvent (Fixture fixtureA, Fixture fixtureB, Contact contact) {

		return true;
	}
	
	
	
	
	
	void OnCollisionSeparation(Fixture fixtureA, Fixture fixtureB) {
		;
	}
	
	
	
	
	void OnTriggerEnter(Collider other) {
	
		/* Am I entering the range of a vortex? */
		if (IsVortex(other.gameObject)) {
			nearVortex = true;	
		}
	}
	
	

	void OnTriggerStay(Collider other) {
		/* If other is the collider of an object you can pick up, then pick it up if possible. */
		//Debug.Log("In OnTriggerStay");
		if (CanPickup(other.gameObject)) {
			if (Input.GetButtonDown("Action1")) {
				Pickup triggeredPickup = other.gameObject.GetComponent<Pickup>();	
				GetPickup(triggeredPickup);	
				currentActionButtonState = ActionButtonStates.CAN_DROP;
			}
			else {
				currentActionButtonState = ActionButtonStates.CAN_PICKUP;	
			}
		}
		/* If other is the collider of a Vortex, then warp if possible. */
		else if (CanWarp()) {
			//Debug.Log ("Near portal!");
			if (Input.GetButtonDown("Action1")) {
				Vortex triggeredVortex = other.gameObject.GetComponent<Vortex>();	
				Warp(triggeredVortex);	
			}
			currentActionButtonState = ActionButtonStates.CAN_ACTIVATE_VORTEX;	
		}
		
		else if (other.tag.CompareTo("Finish") == 0) {
			Debug.Log ("Winner!");
			Application.LoadLevel("scene_prototype_win");
		}
    }
	
	
	
	
	
	void OnTriggerExit(Collider other) {
		currentActionButtonState = ActionButtonStates.NONE;
		
		/* Am I leaving the range of a vortex? */
		if (IsVortex(other.gameObject)) {
			nearVortex = false;	
		}
	}	
	
	
	
	void HandleAnimationCompleted(tk2dSpriteAnimator spriteAnimator, tk2dSpriteAnimationClip clip) {
		
		Debug.Log (clip.name + " completed!!");	
	}
	
	
	
	
	/* If the player's velocity gets larger than the predefined limits, then enforce the
	 * predefined limits by brute force. */
	void HandleVelocityCap() {
	
		FVector2 currentVelocity = body.LinearVelocity;
		FVector2 cappingForce = new FVector2(0f, 0f); // Use this opposing force to enfore the velocity cap.
		
		/* Cap positive/negative x velocity if necessary. */
		if (Mathf.Abs(currentVelocity.X) > maxVelocityX) {
			cappingForce.X = maxVelocityX - currentVelocity.X;
		}
		
		/* Cap positive y velocity if necessary. Don't mess w/ negative y velocity or you might mess up gravity. */
		if (currentVelocity.Y > maxVelocityY) {
			cappingForce.Y = maxVelocityY - currentVelocity.Y;
		}
		
		body.ApplyLinearImpulse(cappingForce);
		
	}
	
	
	
	
	
	/* Handle any additional logic that the player may need to. */
	private void HandleExtraLogic() {
		/* If carrying pickup and can drop it... */
		if (CarryingPickup() && CanDropCarriedPickup()) {
			
			/* Drop pickup if applicable. */
			if (Input.GetButton("Action1")) {
				DropPickup();	
				currentActionButtonState = ActionButtonStates.NONE;
			}
			else {
				currentActionButtonState = ActionButtonStates.CAN_DROP;	
			}
		}
		
		/* Decrement cooldown time remaining for interacting with pickups. */
		pickupCooldownTimeRemaining -= Time.deltaTime;
		pickupCooldownTimeRemaining = Math.Max(0.0f, pickupCooldownTimeRemaining);
		
		
		/* Decrement cooldown time remaining for interacting with vortexes. */
		vortexCooldownTimeRemaining -= Time.deltaTime;
		vortexCooldownTimeRemaining = Math.Max(0.0f, vortexCooldownTimeRemaining);
	}
	
	
	
	
	/* Return true if obj is a Vortex. */
	public bool IsVortex(GameObject obj) {
		return obj.gameObject.CompareTag("Vortex");
	}
	
	
	
	
	/* Return true if obj is a Vortex. */
	public bool CanWarp() {
		return nearVortex && vortexCooldownTimeRemaining == 0;
	}
	
	
	
	
	
	/* Warp the player to another era. */
	private void Warp(Vortex vortex) {
		vortex.OnWarp();
		
		vortexCooldownTimeRemaining = vortexCooldown;
	}
	
	
	
	/* Return true if the player is near a vortex. */
	public bool NearVortex() {
		return nearVortex;	
	}
	
	
	
	
	/* Return true if the player is carrying a pickup. */
	public bool CarryingPickup() {
		return carriedPickup != null;	
	}
	
	
	
	

	/* Can the player pick obj up? */
	private bool CanPickup(GameObject obj) {
		 return obj.gameObject.CompareTag("Pickup") && 
				!CarryingPickup() && 
				obj.GetComponent<Pickup>().CanPickup() && 
				pickupCooldownTimeRemaining == 0;
	}
	
	
	
	

	/* Can the player drop the currently held pickup? */
	private bool CanDropCarriedPickup() {
		if (!CarryingPickup()) {
			throw new Exception("Calling CanDropCarriedPickup() when Player has no held pickup!");	
		}
		return !nearVortex && pickupCooldownTimeRemaining == 0;	
	}
	
	
	
	

	/* Pick up pickup.*/
	private void GetPickup(Pickup pickup) {
		
		Debug.Log(this.name + ": Picking " + pickup.gameObject.name + " up.");
		animator.Play("Plant");
		carriedPickup = pickup;
		pickup.OnPickup(this);	
		
		pickupCooldownTimeRemaining = pickupCooldown;
	}
	
	
	
	
	
	/* Drop held pickup. */
	private void DropPickup() {
		
		Debug.Log(this.name + ": Setting " + carriedPickup.gameObject.name + " down.");
		animator.Play("Plant");
		carriedPickup.OnDrop();	
		carriedPickup = null;
		
		pickupCooldownTimeRemaining = pickupCooldown;
	}
	
	
	
	
	
	/* Returns true if the player is touching the ground. */
	public bool IsGrounded() {
		
		if (currentState.Equals(new ProfessorJumpingState(this))) {
			return ((ProfessorJumpingState)currentState).IsGrounded(); 	
		}
		return body.LinearVelocity.Y == 0.0f;
			
	}
	
	
	
	

	/* Returns true if the player is falling. */
	public bool IsFalling() {
		
		return body.LinearVelocity.Y <= 0;
	}
	
	
	
	
	
	/* Get the next direction that the player is going in. */
	private Direction NextDirection() { 
		
		float horizontalAxis = Input.GetAxis("Horizontal");
		Direction nextDirection;
		
		/* If the player isn't pressing left or right. */
		if (horizontalAxis == 0) {
			/* If the character is in the middle of turning left to right. */
			if (currentDirection == Direction.TURNING_LEFT_TO_RIGHT) {
				nextDirection = Direction.RIGHT;	
			}
			/* If the character is in the middle of turning right to left. */
			else if (currentDirection == Direction.TURNING_RIGHT_TO_LEFT) {
				nextDirection = Direction.LEFT;	
			}
			/* Otherwise, remain in the same direction as the last update. */
			else {
				nextDirection = currentDirection;
			}
		}
		/* If the player is pressing left. */
		else if (horizontalAxis < 0) {
			/* If last direction was right and currently going left, mark as turning right to left. */
			if (currentDirection == Direction.RIGHT) {
				nextDirection = Direction.TURNING_RIGHT_TO_LEFT;	
			}
			else {
				nextDirection = Direction.LEFT;	
			}
		}
		/* If the player is pressing right. */
		else if (horizontalAxis > 0) {
			/* If last direction was left and currently going right, mark as turning left to right. */
			if (currentDirection == Direction.LEFT) {
				nextDirection = Direction.TURNING_LEFT_TO_RIGHT;	
			}
			else {
				nextDirection = Direction.RIGHT;	
			}
		}
		/* If you hit here, you probably fucked up somewhere. */
		else {
			throw new Exception("Invalid direction state!");	
		}
		return nextDirection;

	} // end NextDirection()
}