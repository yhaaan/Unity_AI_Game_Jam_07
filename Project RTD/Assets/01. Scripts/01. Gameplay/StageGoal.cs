using ProjectRTD.Core;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class StageGoal : MonoBehaviour
    {
        private bool completed;
        StageRuntime owner;
        StageGimmickArt entrance, exit;

        internal void BindDoors(StageRuntime stage, StageGimmickArt startDoor, StageGimmickArt endDoor)
        {
            // Dress runs on AfterSceneLoad and again on sceneLoaded for the same scene. A second Arrive
            // would take the already-shrunk player scale as the "normal" one and leave the player tiny.
            if (owner) return;
            owner = stage;
            entrance = startDoor;
            exit = endDoor;
            if (Application.isPlaying) StartCoroutine(Arrive());
        }

        // Evaluate both endpoints even on a slow frame. Scaled time lets the pause menu pause the sequence.
        static System.Collections.IEnumerator Tween(float seconds, System.Action<float> sample)
        {
            for (float elapsed = 0; elapsed < seconds; elapsed += Time.deltaTime)
            {
                sample(Mathf.SmoothStep(0, 1, elapsed / seconds));
                yield return null;
            }
            sample(1);
        }

        System.Collections.IEnumerator Arrive()
        {
            var player = owner.Player;
            var scale = player.localScale;
            var spawn = owner.SpawnPoint.position;
            var inside = entrance.transform.position - Vector3.up * .26f;
            owner.BeginDoorway();
            owner.SetDoorFade(1);
            player.position = inside;
            player.localScale = scale * .001f;
            var overview = StageOverviewCamera.Begin(owner);
            yield return Tween(.48f, t => { owner.SetDoorFade(1-t); entrance.ShowProgress(t); });
            if (overview)
            {
                yield return new WaitForSeconds(.5f);
                yield return Tween(1.1f, t => overview.Weight = 1f - t);
                overview.Weight = 0;   // stays on as the room's camera bounds
            }
            yield return Tween(.45f, t => {
                player.position = Vector3.Lerp(inside, spawn, t);
                player.localScale = scale * Mathf.Lerp(.001f, 1, t);
            });
            owner.EndDoorway();
            yield return Tween(.32f, t => entrance.ShowProgress(1-t));
        }

        System.Collections.IEnumerator Depart()
        {
            var player = owner.Player;
            var scale = player.localScale;
            var start = player.position;
            var inside = exit.transform.position - Vector3.up * .26f;
            owner.BeginDoorway();
            // Star rating for the stage, shown before walking out.
            float stars = StageStars.Show(owner);
            if (stars > 0f) yield return new WaitForSeconds(stars);
            yield return Tween(.45f, exit.ShowProgress);
            yield return Tween(.65f, t => {
                player.position = Vector3.Lerp(start, inside, t);
                player.localScale = scale * Mathf.Lerp(1, .35f, t);
                owner.SetDoorFade(Mathf.InverseLerp(.35f, 1, t));
            });
            // Present the fully black frame before the synchronous scene load.
            yield return null;
            SceneFlow.CompleteActiveStage();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (completed || !other.CompareTag("Player")) return;
            var stage = StageRuntime.Instance;
            if (stage && (stage.IsRespawning || stage.InDoorway)) return;

            completed = true;
            SoundManager.Play(Sfx.Goal);
            if (owner && exit) StartCoroutine(Depart());
            else SceneFlow.CompleteActiveStage();
        }
    }
}
