using System;
using System.Collections.Generic;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using UnityEngine;

namespace LightVsDecay.Logic.Coin
{
    /// <summary>
    /// Spawns pooled visual coins for battle kill rewards.
    /// </summary>
    public class CoinPickupSpawner : Singleton<CoinPickupSpawner>
    {
        [Header("Prefab")]
        [SerializeField] private CoinPickup coinPrefab;

        [Header("Pool")]
        [SerializeField] private int prewarmCount = 24;
        [SerializeField] private int maxCount = 80;

        [Header("Visual Count")]
        [SerializeField] private int maxVisualCoinCount = 8;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        private readonly Queue<CoinPickup> availablePool = new Queue<CoinPickup>();
        private readonly HashSet<CoinPickup> activeCoins = new HashSet<CoinPickup>();

        private Transform poolContainer;
        private Func<Vector3> targetPositionGetter;
        private Action<int> coinArriveNotifier;
        private int totalCreated;

        protected override void OnSingletonAwake()
        {
            CreatePoolContainer();
            InitializePool();
        }

        private void OnEnable()
        {
            GameEvents.OnEnemyDied += OnEnemyDied;
        }

        private void OnDisable()
        {
            GameEvents.OnEnemyDied -= OnEnemyDied;
        }

        protected override void OnSingletonDestroy()
        {
            ClearPool();
        }

        public void SetTargetPositionGetter(Func<Vector3> getter)
        {
            targetPositionGetter = getter;
        }

        public void SetCoinArriveNotifier(Action<int> notifier)
        {
            coinArriveNotifier = notifier;
        }

        public void SpawnCoins(Vector3 position, int coinAmount)
        {
            if (coinAmount <= 0 || coinPrefab == null)
            {
                return;
            }

            int visualCount = CalculateVisualCount(coinAmount);
            int baseValue = coinAmount / visualCount;
            int remainder = coinAmount % visualCount;

            for (int i = 0; i < visualCount; i++)
            {
                CoinPickup coin = GetCoin();
                if (coin == null)
                {
                    break;
                }

                int visualValue = baseValue + (i < remainder ? 1 : 0);
                coin.transform.SetParent(poolContainer, false);
                activeCoins.Add(coin);
                coin.Play(position, visualValue, targetPositionGetter, HandleCoinArrived, ReturnCoin);
            }

            if (showDebugInfo)
            {
                GameLogger.Log($"[CoinPickupSpawner] Spawn {visualCount} visual coins for reward {coinAmount}");
            }
        }

        public void ReturnAllCoins()
        {
            var coins = new List<CoinPickup>(activeCoins);
            for (int i = 0; i < coins.Count; i++)
            {
                ReturnCoin(coins[i]);
            }
        }

        public void ClearPool()
        {
            foreach (CoinPickup coin in activeCoins)
            {
                if (coin != null)
                {
                    Destroy(coin.gameObject);
                }
            }

            activeCoins.Clear();

            while (availablePool.Count > 0)
            {
                CoinPickup coin = availablePool.Dequeue();
                if (coin != null)
                {
                    Destroy(coin.gameObject);
                }
            }

            totalCreated = 0;
        }

        private void OnEnemyDied(EnemyType type, Vector3 pos, int xp, int coin)
        {
            if (coin <= 0)
            {
                return;
            }

            SpawnCoins(pos, coin);
        }

        private void HandleCoinArrived(CoinPickup coin, int visualValue)
        {
            coinArriveNotifier?.Invoke(visualValue);
        }

        private void ReturnCoin(CoinPickup coin)
        {
            if (coin == null || !activeCoins.Contains(coin))
            {
                return;
            }

            activeCoins.Remove(coin);
            coin.gameObject.SetActive(false);
            coin.transform.SetParent(poolContainer, false);
            availablePool.Enqueue(coin);
        }

        private void CreatePoolContainer()
        {
            GameObject container = new GameObject("[CoinPickupPool]");
            container.transform.SetParent(transform, false);
            poolContainer = container.transform;
        }

        private void InitializePool()
        {
            for (int i = 0; i < prewarmCount; i++)
            {
                CoinPickup coin = CreateCoin();
                if (coin == null)
                {
                    break;
                }

                coin.gameObject.SetActive(false);
                availablePool.Enqueue(coin);
            }
        }

        private CoinPickup GetCoin()
        {
            if (availablePool.Count > 0)
            {
                return availablePool.Dequeue();
            }

            if (totalCreated >= maxCount)
            {
                if (showDebugInfo)
                {
                    GameLogger.LogWarning("[CoinPickupSpawner] Pool limit reached, skipping coin visual");
                }

                return null;
            }

            return CreateCoin();
        }

        private CoinPickup CreateCoin()
        {
            if (coinPrefab == null)
            {
                GameLogger.LogError("[CoinPickupSpawner] coinPrefab is not assigned.");
                return null;
            }

            CoinPickup coin = Instantiate(coinPrefab, poolContainer);
            coin.name = $"CoinPickup_{totalCreated:D3}";
            totalCreated++;
            return coin;
        }

        private int CalculateVisualCount(int coinAmount)
        {
            int count;

            if (coinAmount <= 4)
            {
                count = coinAmount;
            }
            else if (coinAmount <= 12)
            {
                count = 4;
            }
            else if (coinAmount <= 24)
            {
                count = 5;
            }
            else if (coinAmount <= 49)
            {
                count = 6;
            }
            else if (coinAmount <= 79)
            {
                count = 7;
            }
            else
            {
                count = 8;
            }

            return Mathf.Clamp(count, 1, Mathf.Max(1, maxVisualCoinCount));
        }
    }
}
