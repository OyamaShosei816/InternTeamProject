using UnityEngine;

namespace Prototype
{
    // MD_Qの各レベルを一組として切り替え、非表示時には軌跡も消す。
    public sealed class CueLevelVisual : MonoBehaviour
    {
        [SerializeField] private GameObject[] levels = new GameObject[3];
        private ParticleSystem[][] particles;
        private TrailRenderer[][] trails;
        private float[][] trailWidths;
        private int activeLevel = -1;

        private void Initialize()
        {
            if (particles != null) return;
            particles = new ParticleSystem[levels.Length][];
            trails = new TrailRenderer[levels.Length][];
            trailWidths = new float[levels.Length][];
            for (int i = 0; i < levels.Length; i++)
            {
                particles[i] = levels[i].GetComponentsInChildren<ParticleSystem>(true);
                trails[i] = levels[i].GetComponentsInChildren<TrailRenderer>(true);
                trailWidths[i] = new float[trails[i].Length];
                for (int j = 0; j < trails[i].Length; j++) trailWidths[i][j] = trails[i][j].widthMultiplier;
                StopLevel(i);
            }
        }

        public void Apply(int power, Vector3 velocity, float hitRange)
        {
            Initialize();
            float stretch = Mathf.Clamp(velocity.magnitude * 0.01f, 0f, 0.2f);
            transform.localScale = new Vector3(1f - stretch * 0.5f, 1f - stretch * 0.5f, 1f + stretch / 0.8f) * hitRange;
            if (velocity.sqrMagnitude > 0.05f) transform.rotation = Quaternion.LookRotation(velocity, Vector3.up);
            int next = Mathf.Clamp(power - 1, 0, levels.Length - 1);
            if (next != activeLevel)
            {
                if (activeLevel >= 0) StopLevel(activeLevel);
                activeLevel = next;
                levels[next].SetActive(true);
                foreach (var trail in trails[next]) { trail.Clear(); trail.emitting = true; }
                foreach (var particle in particles[next]) particle.Play(false);
            }
            for (int i = 0; i < trails[next].Length; i++)
                trails[next][i].widthMultiplier = trailWidths[next][i] * hitRange;
        }

        public void Hide()
        {
            Initialize();
            if (activeLevel >= 0) StopLevel(activeLevel);
            activeLevel = -1;
        }

        private void StopLevel(int index)
        {
            foreach (var particle in particles[index]) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var trail in trails[index]) { trail.emitting = false; trail.Clear(); }
            levels[index].SetActive(false);
        }

        private void OnDisable()
        {
            if (particles != null) Hide();
        }
    }
}
