using System;
using NitroNetwork.Core;
using UnityEngine;

public partial class NitroTransform : NitroBehaviour
{
    // Tick rate for position updates (how often the position is sent to the server)
    public int TickRate;

    // Movement speed of the player
    public float Speed = 5f;

    // Cached reference to the Transform component for performance optimization
    private Transform _transform;

    // Target position the player is moving towards
    private Vector3 moviment;

    // Flags to indicate if the player is moving or has stopped
    private bool IsMoving = true, Stop = false;

    // Timer to track elapsed time for position updates
    private float elapsedTime = 0f;

    // Tolerance for position comparison to avoid float precision issues
    private const float PositionTolerance = 0.01f;

    /// <summary>
    /// Called when the script is initialized.
    /// Caches the Transform component for better performance.
    /// </summary>
    void Start()
    {
        _transform = GetComponent<Transform>();
    }

    /// <summary>
    /// Called every frame to handle player movement and synchronization.
    /// </summary>
    void Update()
    {
        // Logic for the local player
        if (IsMine)
        {
            // Increment elapsed time
            elapsedTime += Time.deltaTime;

            // Check if it's time to send a position update based on TickRate
            if (elapsedTime >= 1f / TickRate)
            {
                // If the player's position has changed, notify the server
                if (!IsPositionEqual(transform.position, moviment))
                {
                    Stop = false; // Player is moving
                    CallReceiveMove(transform.position); // Notify the server of the new position
                }
                // If the player has stopped moving, notify the server
                else if (!Stop && IsPositionEqual(transform.position, moviment))
                {
                    Stop = true; // Player has stopped
                    CallStopServeR(transform.position); // Notify the server of the stop
                }

                // Reset the elapsed time
                elapsedTime = 0f;
            }

            // Update the target position to the current position
            moviment = transform.position;
        }

        // Logic for remote players
        if (!IsMine && IsMoving)
        {
            // Smoothly move the player towards the target position
            _transform.position = Vector3.MoveTowards(_transform.position, moviment, Speed * Time.deltaTime);

            // Check if the player has reached the target position
            if (IsPositionEqual(_transform.position, moviment))
            {
                _transform.position = moviment; // Snap to the target position
                IsMoving = false; // Stop the movement
            }
        }
    }

    /// <summary>
    /// Compares two positions with a tolerance to avoid float precision issues.
    /// </summary>
    /// <param name="pos1">The first position.</param>
    /// <param name="pos2">The second position.</param>
    /// <returns>True if the positions are approximately equal, false otherwise.</returns>
    private bool IsPositionEqual(Vector3 pos1, Vector3 pos2)
    {
        return Vector3.Distance(pos1, pos2) <= PositionTolerance;
    }

    /// <summary>
    /// Called by the server to notify that the player has stopped moving.
    /// Updates the target position but does not adjust the player's position directly.
    /// </summary>
    /// <param name="move">The position where the player stopped.</param>
    [NitroRPC(RPC.Server)]
    void StopServeR(Vector3 move)
    {
        moviment = move; // Update the target position
        CallReceiveMoveStop(move); // Notify other clients
    }

    /// <summary>
    /// Called by the server to update the player's position.
    /// Starts the movement towards the new position.
    /// </summary>
    /// <param name="move">The new position to move towards.</param>
    [NitroRPC(RPC.Server, deliveryMode = DeliveryMode.Sequenced)]
    void ReceiveMove(Vector3 move)
    {
        moviment = move; // Update the target position
        IsMoving = true; // Start moving
        CallReceiveMoveInEnemy(transform.position); // Notify other clients
    }

    /// <summary>
    /// Called by the server to notify other clients that the player has stopped moving.
    /// Updates the target position but does not adjust the player's position directly.
    /// </summary>
    /// <param name="move">The position where the player stopped.</param>
    [NitroRPC(RPC.Client, target = Target.ExceptSelf)]
    void ReceiveMoveStop(Vector3 move)
    {
        moviment = move; // Update the target position
    }

    /// <summary>
    /// Called by the server to update the position of remote players.
    /// Starts the movement towards the new position.
    /// </summary>
    /// <param name="move">The new position to move towards.</param>
    [NitroRPC(RPC.Client, target = Target.ExceptSelf, deliveryMode = DeliveryMode.Sequenced)]
    void ReceiveMoveInEnemy(Vector3 move)
    {
        moviment = move; // Update the target position
        IsMoving = true; // Start moving
    }
}
