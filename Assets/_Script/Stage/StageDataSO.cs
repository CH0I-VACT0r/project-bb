using UnityEngine;

[CreateAssetMenu(fileName = "New Stage Data", menuName = "ScriptableObjects/StageData")]
public class StageDataSO : ScriptableObject
{
    public int stageId; // 예: 1, 2, 3

    [Header("Environment")]
    public GameObject mapPrefab; // 씬에 깔릴 타일맵/배경 프리팹 (NetworkObject 부착 필수)

    [Header("Spawning System")]
    public GameObject[] normalEnemyPrefabs; // 이 스테이지에 등장할 일반 몬스터들
    public GameObject bossPrefab; // 스테이지 보스
    public int waveCount = 3; // 웨이브 횟수
    public float timeBetweenWaves = 10f; // 웨이브 간격
}