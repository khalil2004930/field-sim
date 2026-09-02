using System.Collections.Generic;
using UnityEngine;

namespace FieldSim.Unity.Environment.Entities
{
    public enum FpvFlightState
    {
        Stowed,
        Launching,
        Airborne,
        Holding,
        Landed,
        Expended
    }

    /// <summary>
    /// Environment-only FPV motion. Target selection and combat decisions remain in SimCore.
    /// </summary>
    public sealed class FpvDroneEntity : WorldEntity
    {
        [SerializeField] private FpvFlightState state = FpvFlightState.Stowed;
        [SerializeField] private float maxSpeedMetersPerSecond = 28f;
        [SerializeField] private float accelerationMetersPerSecondSquared = 4.5f;
        [SerializeField] private float waypointToleranceMeters = 1.5f;
        [SerializeField] private List<Vector3> waypoints = new List<Vector3>();

        private int waypointIndex;
        private float speed;

        public FpvFlightState State => state;
        public float SpeedMetersPerSecond => speed;

        public void SetWaypoints(IEnumerable<Vector3> route)
        {
            waypoints.Clear();
            if (route != null)
            {
                waypoints.AddRange(route);
            }
            waypointIndex = 0;
        }

        public void Launch()
        {
            if (state != FpvFlightState.Stowed && state != FpvFlightState.Landed)
            {
                return;
            }

            state = FpvFlightState.Launching;
            speed = 0f;
        }

        public void MarkExpended()
        {
            state = FpvFlightState.Expended;
            speed = 0f;
        }

        private void FixedUpdate()
        {
            if (state == FpvFlightState.Launching)
            {
                state = FpvFlightState.Airborne;
            }

            if (state != FpvFlightState.Airborne || waypoints.Count == 0)
            {
                return;
            }

            Vector3 target = waypoints[Mathf.Clamp(waypointIndex, 0, waypoints.Count - 1)];
            Vector3 delta = target - transform.position;
            float distance = delta.magnitude;

            if (distance <= waypointToleranceMeters)
            {
                waypointIndex++;
                if (waypointIndex >= waypoints.Count)
                {
                    state = FpvFlightState.Holding;
                    speed = 0f;
                    return;
                }

                target = waypoints[waypointIndex];
                delta = target - transform.position;
                distance = delta.magnitude;
            }

            speed = Mathf.MoveTowards(speed, maxSpeedMetersPerSecond, accelerationMetersPerSecondSquared * Time.fixedDeltaTime);
            Vector3 direction = distance > 0.001f ? delta / distance : Vector3.zero;
            float step = Mathf.Min(distance, speed * Time.fixedDeltaTime);
            transform.position += direction * step;

            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }
    }
}
