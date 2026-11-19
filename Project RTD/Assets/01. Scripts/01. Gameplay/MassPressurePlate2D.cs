using System.Collections.Generic;
using ProjectRTD.Core;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class MassPressurePlate2D : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float requiredMass = 2.5f;
        [SerializeField, Min(0f)] private float doorSpeed = 5f;
        [SerializeField] private Transform door;
        [SerializeField] private Vector3 closedDoorPosition;
        [SerializeField] private Vector3 openDoorPosition;
        [SerializeField] private Rigidbody2D requiredBody;

        private readonly HashSet<Rigidbody2D> bodies = new();
        private SpriteRenderer plateRenderer;
        private StageRuntime stage;

        public float CurrentMass { get; private set; }
        public bool IsPressed => CurrentMass >= requiredMass;
        internal Transform Door => door;
        internal Vector3 ClosedDoorPosition => closedDoorPosition;

        /// <summary>0 = door at its closed position, 1 = fully open.</summary>
        internal float DoorOpen
        {
            get
            {
                if (!door) return 0f;
                float travel = Vector3.Distance(closedDoorPosition, openDoorPosition);
                return travel > 0f ? Mathf.Clamp01(Vector3.Distance(closedDoorPosition, door.position) / travel) : 0f;
            }
        }

        /// <summary>Keeps the closed position and makes the door open straight up by <paramref name="rise"/>.</summary>
        internal void SetOpenRise(float rise) => openDoorPosition = closedDoorPosition + Vector3.up * rise;

        public void Configure(
            Transform doorTransform,
            Vector3 closedPosition,
            Vector3 openPosition,
            float massThreshold)
        {
            door = doorTransform;
            closedDoorPosition = closedPosition;
            openDoorPosition = openPosition;
            requiredMass = Mathf.Max(0f, massThreshold);
        }

        private void Awake()
        {
            plateRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            stage = StageRuntime.Instance;
            if (stage) stage.ResetMechanics += ResetPlate;
        }

        private void OnDestroy()
        {
            if (stage) stage.ResetMechanics -= ResetPlate;
        }

        private void ResetPlate()
        {
            bodies.Clear();
            CurrentMass = 0f;
            if (plateRenderer) plateRenderer.color = PrototypePalette.Danger;
            if (door) door.position = closedDoorPosition;
        }

        private void FixedUpdate()
        {
            bodies.RemoveWhere(body => body == null || !body.simulated);

            bool wasPressed = IsPressed;
            CurrentMass = 0f;
            foreach (Rigidbody2D body in bodies)
            {
                CurrentMass += body.mass;
            }

            if (wasPressed != IsPressed) SoundManager.Play(IsPressed ? Sfx.PlateDown : Sfx.PlateUp);

            if (plateRenderer != null)
            {
                plateRenderer.color = IsPressed ? PrototypePalette.Goal : PrototypePalette.Danger;
            }

            if (door != null)
            {
                Vector3 target = IsPressed ? openDoorPosition : closedDoorPosition;
                door.position = Vector3.MoveTowards(door.position, target, doorSpeed * Time.fixedDeltaTime);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Rigidbody2D body = other.attachedRigidbody;
            if (body != null && body.bodyType == RigidbodyType2D.Dynamic && (!requiredBody || body == requiredBody))
            {
                bodies.Add(body);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Rigidbody2D body = other.attachedRigidbody;
            if (body != null)
            {
                bodies.Remove(body);
            }
        }
    }
}
