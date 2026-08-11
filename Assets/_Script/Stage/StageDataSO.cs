using UnityEngine;

[System.Serializable]

public struct MonsterSpawnGroup
{
    [Tooltip("스폰할 몬스터 프리팹")]
    public GameObject monsterPrefab;
    [Tooltip("이 몬스터를 연속으로 스폰할 마릿수")]
    public int count;
}

[System.Serializable]
public struct WaveSpawnData
{
    [Tooltip("이 웨이브에서 순서대로 스폰할 몬스터 그룹 목록")]
    public MonsterSpawnGroup[] spawnGroups;
    [Tooltip("다음 웨이브까지의 대기 시간 (초)")]
    public float timeToNextWave;
}

[System.Serializable]
public struct FloorSpawnData
{
    public int targetFloor; // 예: 5층
    [Tooltip("웨이브별 상세 스폰 설정 (보스/엘리트 층은 1개 웨이브에 1마리 설정)")]
    public WaveSpawnData[] waves;
}

[CreateAssetMenu(fileName = "Chapter1_StageData", menuName = "ScriptableObjects/StageData")]
public class StageDataSO : ScriptableObject
{
    [Header("Chapter Info")]
    public int chapterId = 1;

    [Header("1. Room Environment Maps")]
    public GameObject defaultCombatMap;
    public GameObject eliteCombatMap;
    public GameObject bossCombatMap;
    public GameObject healRoomMap;
    public GameObject shopRoomMap;

    [Header("2. Floor by Floor Spawning DB")]
    [Tooltip("기본 스폰 설정 (커스텀 설정이 없는 일반 층에 적용)")]
    public FloorSpawnData defaultSpawnData;
    [Tooltip("일반 전투 층 진행 단계마다 추가로 증가시킬 총 몬스터 마릿수 (예: 3 입력 시 2층은 +3마리, 3층은 +6마리)")]
    public int additionalMonstersPerStageStep = 3;
    [Tooltip("특별한 층 (5, 10, 15층 엘리트 / 20층 보스 / 특별 웨이브 등) 커스텀 설정")]
    public FloorSpawnData[] customFloorSpawnData;

    public FloorSpawnData GetFloorData(int floor)
    {
        // 1. 커스텀 설정이 등록된 특별 층(엘리트/보스/특수 웨이브)이면 해당 데이터를 원본 그대로 반환
        if (customFloorSpawnData != null)
        {
            foreach (var data in customFloorSpawnData)
            {
                if (data.targetFloor == floor) return data;
            }
        }

        // 2. 일반 전투 층일 경우: defaultSpawnData를 복제한 뒤 단계별로 마릿수를 증가시켜 반환
        return CreateScaledDefaultData(floor);
    }

    private FloorSpawnData CreateScaledDefaultData(int floor)
    {
        // 5층 주기 기준 현재 전투 단계 계산 (예: 1층->0, 2층->1, 3층->2 / 6층->0, 7층->1, 8층->2)
        int step = (floor - 1) % 5;
        if (step > 2) step = 0; // 4층(Heal/Shop), 5층(Boss/Elite) 방어 코드

        int totalBonusMonsters = step * additionalMonstersPerStageStep;

        // 원본 템플릿 복사
        FloorSpawnData scaledData = new FloorSpawnData
        {
            targetFloor = floor,
            waves = new WaveSpawnData[defaultSpawnData.waves.Length]
        };

        for (int w = 0; w < defaultSpawnData.waves.Length; w++)
        {
            WaveSpawnData originWave = defaultSpawnData.waves[w];
            WaveSpawnData newWave = new WaveSpawnData
            {
                timeToNextWave = originWave.timeToNextWave,
                spawnGroups = new MonsterSpawnGroup[originWave.spawnGroups.Length]
            };

            for (int g = 0; g < originWave.spawnGroups.Length; g++)
            {
                MonsterSpawnGroup originGroup = originWave.spawnGroups[g];

                // 마지막 그룹 또는 첫 번째 그룹에 보너스 마릿수 합산 (기획에 맞게 분배 가능)
                int bonus = (g == 0) ? totalBonusMonsters : 0;

                newWave.spawnGroups[g] = new MonsterSpawnGroup
                {
                    monsterPrefab = originGroup.monsterPrefab,
                    count = originGroup.count + bonus
                };
            }

            scaledData.waves[w] = newWave;
        }

        return scaledData;
    }
}