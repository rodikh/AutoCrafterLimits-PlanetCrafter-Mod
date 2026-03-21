using System;
using System.Collections;
using SpaceCraft;
using UnityEngine;

namespace AutoCrafterLimits
{
    /// <summary>
    /// Subscribes to the game's save event to persist our config when the player saves (or auto-save runs).
    /// </summary>
    internal sealed class GameSaveListener : MonoBehaviour
    {
        private bool _subscribed;

        private void Start()
        {
            StartCoroutine(TrySubscribeWhenReady());
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private IEnumerator TrySubscribeWhenReady()
        {
            while (!_subscribed)
            {
                yield return null;

                SavedDataHandler handler = Managers.GetManager<SavedDataHandler>();
                if (handler != null)
                {
                    handler.OnSaved += OnGameSaved;
                    _subscribed = true;
                    break;
                }
            }
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            SavedDataHandler handler = Managers.GetManager<SavedDataHandler>();
            if (handler != null)
            {
                handler.OnSaved -= OnGameSaved;
            }

            _subscribed = false;
        }

        private void OnGameSaved()
        {
            ModRuntime.Store?.Save();
        }
    }
}
