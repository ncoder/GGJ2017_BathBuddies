﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.VR;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class MainGame : MonoBehaviourSingleton<MainGame> {
	 
	public enum State {
		Playing = 0,
		Won = 1,
		Lost = 2,
	}

    [SerializeField] private Turret m_TurretPrefab;
	public WaveSpecSO Waves;
	public int WaveNumber = 0;
	public State GameState = State.Playing;

	public List<GameObject> SpawnPointsAir = new List<GameObject>();
	public List<GameObject> SpawnPointsUnderWater = new List<GameObject>();

    private Transform shipsContainer;

    public static Transform enemyBulletsContainer;
	public static Transform playerBulletsContainer;
    private Transform m_TowersContainer;

	private GameObject m_SonarOverlayGroup;

	public static float GameTime()
	{
		return Time.time;
	}

	private float _multipleToSpeedUpTimeBy = 10f;

	[ContextMenu("Make invincible, kill current wave, speed up time")]
	private void TestDesign()
	{
		
		StopCoroutine("TriggerWave");
		killEverything();

		Time.timeScale = _multipleToSpeedUpTimeBy;
		GameObject.FindObjectOfType<BaseStats>().health = int.MaxValue;

	}

	[ContextMenu("Make time normal", false, 1000001)]
	public void NormalizeTime()
	{
		Time.timeScale = 1f;
	}

	private float _gameStartedTime;

	void Start ()
	{
		m_SonarOverlayGroup = GameObject.FindWithTag("FlatscreenPlayer");
		_gameStartedTime = Time.time;
        enemyBulletsContainer = new GameObject("enemy-bullets-container").transform;
		playerBulletsContainer = new GameObject("player-bullets-container").transform;
        m_TowersContainer = new GameObject("towers-container").transform;
        VRPlayer.TeleportTo(TurretManager.startingTurret.teleportPoint);

		FindObjectOfType<BaseStats>().OnDie += OnBaseDied;

        shipsContainer = new GameObject("ships-container").transform;
		StartCoroutine(pollWaves());
	}

	private void OnBaseDied(Stats stats)
	{
		TriggerLose();
	}

	public bool IsGameOver()
	{
		return GameState >= State.Won;
	}
	
	// Update is called once per frame
	void Update () {

		if(IsGameOver())
			return;
			
		if(WaveNumber >= Waves.Waves.Count)
		{
			TriggerWin();
		}
		if (m_SonarOverlayGroup == null)
			return;
		if (Input.GetKeyDown(KeyCode.Tab))
		{
			m_SonarOverlayGroup.SetActive(false);
		}
		else if (Input.GetKeyUp(KeyCode.Tab))
		{
			m_SonarOverlayGroup.SetActive(true);
		}
	}

	public IEnumerator pollWaves()
	{
		while (!IsGameOver())
		{
			var nextWave = Waves.Waves[WaveNumber];
			//if(nextWave.StartTime_s <= GameTime()) //FIXME: gametime doesn't 
			//{
				var blockUntilNextDone = nextWave.blockUntilAllEnemiesAreDead;
				var coroutine = StartCoroutine(TriggerWave(nextWave));
				if(blockUntilNextDone) { 
					yield return coroutine;
					WinningDuh();

				}
				WaveNumber++;
			//}
			yield return null;
		}
	}

#warning "Celebrate winning here"
	[ContextMenu("Winning")]
	private void WinningDuh()
	{
		StartCoroutine(spawnWinEffect());
	}

	IEnumerator spawnWinEffect()
	{
		var go = GameObject.Instantiate(Resources.Load("confetti2"),InputTracking.GetLocalPosition(VRNode.Head),Quaternion.identity) as GameObject;
		Debug.Log("Spawning confetti",go);
		yield return new WaitForSeconds(10f);
		GameObject.Destroy(go);
	}
	
	public Vector3 spawnPointInsideOfBoundsForEnemySpec(EnemySpec spec)
	{
		var targetSpawnPointCollection = spec.underwater ? SpawnPointsUnderWater : SpawnPointsAir;
		Collider spawnPointArea = null;
		//find the corner that is tagged with the same enum as the group
		foreach (var collection in targetSpawnPointCollection)
		{
			foreach (Transform spawnPointTransform in collection.transform)
			{
				var spawnPoint = spawnPointTransform.gameObject;
				var spawnTag = spawnPoint.GetComponent<EnemySpawnAreaTag>();
				if(spawnTag == null) {
					Debug.LogError(spawnPoint.name + " does not ahave a spawntag!");
					continue;
				}
				var corner = spawnTag.Corner;
				if(corner == spec.spawnCorner) {
					spawnPointArea = spawnPoint.GetComponent<EnemySpawnAreaTag>().GetComponent<Collider>();
				}
			}
			
		}
		
		//null ref means that the air or watter pool wasn't taged with one of the enemy spawn area tags
		return RandomEx.InsideBounds(spawnPointArea.bounds);
	}

	IEnumerator TriggerWave(WaveSpec spec)
	{
		Debug.Log("Start wave");
		float waveStartedAt = Time.time;
		Dictionary<EnemySpec,int> enemiesSpawned = new Dictionary<EnemySpec, int>();
		Dictionary<EnemySpec, int> enemiesSpawnedLastFrame = new Dictionary<EnemySpec, int>();  //not braining. this will do.
		List<GameObject> objectsSpawned = new List<GameObject>();

		foreach (var enemyWeights in spec.EnemyWeights)
		{
			enemiesSpawned.Add(enemyWeights, 0);
		}
		
		while (Time.time - waveStartedAt < spec.DurationOfWave)
		{
			float normalizedTime = (Time.time - waveStartedAt)/spec.DurationOfWave;

			foreach (KeyValuePair<EnemySpec, int> keyValuePair in enemiesSpawned) //yes, gc. yes, gamejam.
			{
				var enemySpec = keyValuePair.Key;
				var enemiesOfThisTypeSpawnedPreviously = keyValuePair.Value;

				float accordingToTime = enemySpec.rampUpOfEnemiesInCurve.Evaluate(normalizedTime);
				//Debug.Log(accordingToTime);
				int expectedEnmiesNow = Mathf.FloorToInt(accordingToTime * enemySpec.NumEnemies);
				int newEnemiesToSpawn = expectedEnmiesNow - enemiesOfThisTypeSpawnedPreviously;

				//TODO: watch out for collection modified exception here
				enemiesSpawnedLastFrame[enemySpec] = newEnemiesToSpawn;

				//Debug.LogFormat("expected {0} newTOSpawn {1}",expectedEnmiesNow,);
				for(int i = 0;i < newEnemiesToSpawn;i++)
				{
					var go = Instantiate(enemySpec.prefab, spawnPointInsideOfBoundsForEnemySpec(enemySpec), Quaternion.identity);
					go.transform.parent = shipsContainer;
					objectsSpawned.Add(go);
					//Debug.Log("Spawned", go);
				}
			}
			
			foreach (var lastFrameAdds in enemiesSpawnedLastFrame)
			{
				enemiesSpawned[lastFrameAdds.Key] += lastFrameAdds.Value;
			}
			
			yield return null;
		}
		foreach(var keyValuePair in enemiesSpawned) {
			var enemySpec = keyValuePair.Key;
			var enemiesOfThisTypeSpawnedPreviously = keyValuePair.Value;
			int expectedEnmiesNow = enemySpec.NumEnemies;
			int newEnemiesToSpawn = expectedEnmiesNow - enemiesOfThisTypeSpawnedPreviously;
			for(int i = 0;i < newEnemiesToSpawn;i++) {
				var go = Instantiate(enemySpec.prefab, spawnPointInsideOfBoundsForEnemySpec(enemySpec), Quaternion.identity);
				go.transform.parent = shipsContainer;
				objectsSpawned.Add(go);
				Debug.Log("Spawned at end", go);
			}
		}
		Debug.Log("wave spawned all, waiting until all enemies died");
		//wait until this wave is "done"
		while(true) { 
			bool allEnemiesCleared = true;
			foreach (var enemy in objectsSpawned)
			{
				if(enemy != null) { 
					allEnemiesCleared = false;
					break;
				}
			}
			if(allEnemiesCleared)
				break;
			yield return null;
		}
		WinningDuh();
		Debug.Log("wave done");
		//NOTE: this may cut off the last few in some cases. can be fixed in data
	}
		
	void TriggerWin()
	{
		GameState = State.Won;
		// todo;		
	}

	void killEverything()
	{
		foreach(var stats in FindObjectsOfType<EnemyStats>()) {
			if(stats != null) //in the process of being cleaned up?
				Destroy(stats.gameObject);
			//Debug.Log("TriggerLose Destroying:" +stats.gameObject);
		}
		foreach(var turret in FindObjectsOfType<Turret>()) {
			if(turret != null) //in the process of being cleaned up?
				Destroy(turret.gameObject);
			//Debug.Log("TriggerLose Destroying:" +stats.gameObject);
		}
		foreach(var projectile in FindObjectsOfType<Projectile>()) {
			if(projectile != null) //in the process of being cleaned up?
				Destroy(projectile.gameObject);
			//Debug.Log("TriggerLose Destroying:" +stats.gameObject);
		}
	}
	void TriggerLose()
	{
		GameState = State.Lost;
		//FIXME: just destroy all things under bulletsContainer, et al
		killEverything();
		//stopping coroutine by string name. Not awesome
		StopCoroutine("TriggerWave");
		GameObject.FindObjectOfType<CrossGameState>().OnGameOver(new CrossGameState.ScoreInfo() {ScoreThisRun = WaveNumber ,TimeAlive = Time.time - _gameStartedTime});
		// todo;
	}



}
