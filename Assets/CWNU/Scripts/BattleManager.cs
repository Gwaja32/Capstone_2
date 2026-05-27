using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance;

    [Header("Data Pass (Selected Player Name)")]
    public string selectedCharacterName;
    public string enemyCharacterName;

    [Header("Scene Settings")]
    public string selectSceneName = "Select";
    public string battleSceneName = "Test";

    // 🔴 인스펙터 등록 필요 없음 (코드가 Resources 폴더에서 자동 로드)
    private GameObject enemyControllerPrefab;

    private GameObject spawnedEnemyController;
    private EnemyAI currentEnemyAIScript;

    [Header("Enemy Models Pool")]
    public List<string> enemyModelNames = new List<string>();

    [Header("Stage Progress Info")]
    public int currentStage = 1;
    public bool isFinalStage = false;

    [Header("UI References (Auto Cached)")]
    private GameObject gameOverUIPanel;
    private GameObject gameClearUIPanel;
    private Image enemyHPBarImage;
    private Image enemyStaminaBarImage;

    private Image playerPortraitImage;
    private Image enemyPortraitImage;

    private List<string> remainingEnemies;
    private GameObject currentActiveModel;
    private bool isInitialized = false;
    private bool isGameOver = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeEnemyPool();

            // 🔴 게임 시작 시 딱 한 번 Resources 폴더에서 프리팹을 강제로 메모리에 로드해둡니다.
            LoadEnemyPrefabFromResources();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    // 🔴 Resources 폴더에서 프리팹을 다이렉트로 꽂아버리는 안전장치
    private void LoadEnemyPrefabFromResources()
    {
        if (enemyControllerPrefab == null)
        {
            enemyControllerPrefab = Resources.Load<GameObject>("EnemyController");

            if (enemyControllerPrefab != null)
            {
                Debug.Log("📦 [BattleManager] Resources 폴더에서 EnemyController 프리팹 자동 로드 성공!");
            }
            else
            {
                Debug.LogError("🚨 [BattleManager] 에러: Resources 폴더 안에서 'EnemyController' 프리팹을 찾을 수 없습니다! 폴더 이름과 파일명을 확인해주세요.");
            }
        }
    }

    private void InitializeEnemyPool()
    {
        remainingEnemies = new List<string>(enemyModelNames);
        currentStage = 1;
        isFinalStage = false;
        isInitialized = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != this) return;

        if (scene.name == battleSceneName)
        {
            isGameOver = false;

            // 🔴 씬이 전환될 때 유니티가 혹시나 변수를 날렸을까봐 한 번 더 강제로 로드해줍니다.
            LoadEnemyPrefabFromResources();

            StartCoroutine(BattleSetupRoutine());
        }
    }

    private IEnumerator BattleSetupRoutine()
    {
        yield return null;

        // 1. Spawner 위치에 적 프리팹 생성
        SpawnEnemyControllerAtSpawner();

        // 2. UI 및 캔버스 요소 캐싱
        InitializeBattleStage();

        // 3. 적에게 플레이어 정보 전달
        RefreshPlayerReferenceInEnemy();
    }

    private void SpawnEnemyControllerAtSpawner()
    {
        if (enemyControllerPrefab == null)
        {
            Debug.LogError("🚨 [BattleManager] 프리팹 로드에 최종 실패했습니다. 코드가 뻗는 걸 방지합니다.");
            return;
        }

        GameObject spawnPoint = GameObject.Find("EnemySpawnPoint");
        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        if (spawnPoint != null)
        {
            spawnPosition = spawnPoint.transform.position;
            spawnRotation = spawnPoint.transform.rotation;
        }

        // 스폰 실행
        spawnedEnemyController = Instantiate(enemyControllerPrefab, spawnPosition, spawnRotation);

        if (spawnedEnemyController != null)
        {
            spawnedEnemyController.name = "EnemyController_Spawned";
            Debug.Log("✅ [BattleManager] Resources 에셋 기반으로 Spawner 좌표에 적 정상 스폰 성공!");
        }
    }

    private void RefreshPlayerReferenceInEnemy()
    {
        if (currentEnemyAIScript != null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                currentEnemyAIScript.playerTransform = playerObj.transform;
            }
        }
    }

    private void LoadBattleScene() { SceneManager.LoadScene(battleSceneName); }

    public void SelectAlix() { selectedCharacterName = "Alix"; LoadBattleScene(); }
    public void SelectEcho() { selectedCharacterName = "Echo"; LoadBattleScene(); }
    public void SelectGorr() { selectedCharacterName = "Gorr"; LoadBattleScene(); }

    private void InitializeBattleStage()
    {
        GameObject mainCanvas = GameObject.Find("AttackDirectionCanvas");

        if (mainCanvas != null)
        {
            Transform gameOverGroupTF = mainCanvas.transform.Find("GameOverGroup");
            if (gameOverGroupTF != null)
            {
                gameOverUIPanel = gameOverGroupTF.gameObject;
                gameOverUIPanel.SetActive(false);
            }

            Transform gameClearGroupTF = mainCanvas.transform.Find("GameClearGroup");
            if (gameClearGroupTF != null)
            {
                gameClearUIPanel = gameClearGroupTF.gameObject;
                gameClearUIPanel.SetActive(false);
            }

            Transform enemyBarTF = mainCanvas.transform.Find("StatusUIGroup/Enemy_Bar");
            if (enemyBarTF != null)
            {
                Transform hpTF = enemyBarTF.Find("health");
                Transform staminaTF = enemyBarTF.Find("stamina");

                if (hpTF != null) enemyHPBarImage = hpTF.GetComponent<Image>();
                if (staminaTF != null) enemyStaminaBarImage = staminaTF.GetComponent<Image>();
            }

            Transform playerPortraitTF = mainCanvas.transform.Find("StatusUIGroup/Player_Bar/Portrait");
            Transform enemyPortraitTF = mainCanvas.transform.Find("StatusUIGroup/Enemy_Bar/Portrait");

            if (playerPortraitTF != null) playerPortraitImage = playerPortraitTF.GetComponent<Image>();
            if (enemyPortraitTF != null) enemyPortraitImage = enemyPortraitTF.GetComponent<Image>();
        }

        if (!isInitialized || remainingEnemies == null)
        {
            InitializeEnemyPool();
        }

        // -----------------------------------------------------------------
        // 🔴 [수정] 여기서 원래 있던 StartNextStage(); 를 과감하게 지웁니다!
        // -----------------------------------------------------------------

        // 1. 만약 Select 씬에서 뽑아둔 적 이름이 없다면(방어 코드), 여기서 하나 뽑아줍니다.
        if (string.IsNullOrEmpty(enemyCharacterName))
        {
            int randomIndex = Random.Range(0, remainingEnemies.Count);
            enemyCharacterName = remainingEnemies[randomIndex];
        }

        // 2. 이미 스테이지 풀(remainingEnemies)에 들어있는 녀석이라면 다음 스테이지를 위해 리스트에서 제거합니다.
        if (remainingEnemies.Contains(enemyCharacterName))
        {
            remainingEnemies.Remove(enemyCharacterName);
        }

        // 3. 최종적으로 '선택창에서 뽑힌 적'의 모델을 맵에 활성화합니다!
        ActivateEnemyModelByName(enemyCharacterName);
        ApplyPortraits();
    }

    /// <summary>
    /// 🔴 [새로 추가된 함수] 
    /// Resources 폴더에서 현재 플레이어와 적 이름에 맞는 초상화를 가져와 UI에 적용합니다.
    /// </summary>
    private void ApplyPortraits()
    {
        // 1. 플레이어 초상화 적용
        if (playerPortraitImage != null && !string.IsNullOrEmpty(selectedCharacterName))
        {
            Sprite playerSprite = Resources.Load<Sprite>($"Portraits/{selectedCharacterName}");
            if (playerSprite != null) playerPortraitImage.sprite = playerSprite;
        }

        // 2. 적 초상화 적용
        if (enemyPortraitImage != null && !string.IsNullOrEmpty(enemyCharacterName))
        {
            Sprite enemySprite = Resources.Load<Sprite>($"Portraits/{enemyCharacterName}");
            if (enemySprite != null) enemyPortraitImage.sprite = enemySprite;
        }
    }

    public void StartNextStage()
    {
        if (remainingEnemies.Count == 0) return;
        if (remainingEnemies.Count == 1) isFinalStage = true;

        int randomIndex = Random.Range(0, remainingEnemies.Count);
        string selectedEnemyName = remainingEnemies[randomIndex];
        remainingEnemies.RemoveAt(randomIndex);

        ActivateEnemyModelByName(selectedEnemyName);
    }

    private void ActivateEnemyModelByName(string modelName)
    {
        if (spawnedEnemyController == null) return;

        bool foundModel = false;
        foreach (Transform child in spawnedEnemyController.transform)
        {
            if (child.name == "Target_Enemy") continue;

            if (child.name == modelName)
            {
                child.gameObject.SetActive(true);
                currentActiveModel = child.gameObject;
                foundModel = true;
            }
            else
            {
                child.gameObject.SetActive(false);
            }
        }

        if (foundModel && currentActiveModel != null)
        {
            currentEnemyAIScript = currentActiveModel.GetComponent<EnemyAI>();

            if (currentEnemyAIScript != null)
            {
                currentEnemyAIScript.anim = currentActiveModel.GetComponent<Animator>();
                currentEnemyAIScript.currentState = EnemyAI.AIState.Idle;
                currentEnemyAIScript.currentHealth = currentEnemyAIScript.maxHealth;
                currentEnemyAIScript.currentStamina = currentEnemyAIScript.maxStamina;

                Transform targetEnemyTransform = currentActiveModel.transform.FindDeepChild("Target_Enemy");
                if (targetEnemyTransform == null)
                {
                    targetEnemyTransform = spawnedEnemyController.transform.FindDeepChild("Target_Enemy");
                }

                PlayerController playerCtrl = FindFirstObjectByType<PlayerController>();
                if (playerCtrl != null && targetEnemyTransform != null)
                {
                    playerCtrl.SetLockOnTarget(targetEnemyTransform);
                }

                if (enemyHPBarImage != null && enemyStaminaBarImage != null)
                {
                    SimpleStatusUI statusUI = currentActiveModel.GetComponent<SimpleStatusUI>();
                    if (statusUI != null)
                    {
                        statusUI.SetupUI(enemyHPBarImage, enemyStaminaBarImage, currentEnemyAIScript);
                    }
                }
            }
        }
    }

    public void OnEnemyDefeated()
    {
        if (isGameOver) return;

        PlayerController playerCtrl = FindFirstObjectByType<PlayerController>();
        if (playerCtrl != null) playerCtrl.SetLockOnTarget(null);

        if (isFinalStage || currentStage == 3)
        {
            OnGameClear();
            return;
        }

        currentStage++;
        StartCoroutine(BackToSelectSceneRoutine());
    }

    private IEnumerator BackToSelectSceneRoutine()
    {
        yield return new WaitForSeconds(3f);
        SceneManager.LoadScene(selectSceneName);
    }

    public void OnPlayerDefeated()
    {
        if (isGameOver) return;
        isGameOver = true;

        PlayerController playerCtrl = FindFirstObjectByType<PlayerController>();
        if (playerCtrl != null) playerCtrl.SetLockOnTarget(null);

        if (gameOverUIPanel != null) gameOverUIPanel.SetActive(true);

        StartCoroutine(WaitForKeyPressAndGoToLobby());
    }

    private void OnGameClear()
    {
        if (gameClearUIPanel != null) gameClearUIPanel.SetActive(true);
        SoundManager.Instance.PlaySingleSFX(SoundManager.Instance.clearSound, 1f);

        StartCoroutine(WaitForKeyPressAndGoToLobby());
    }

    private IEnumerator WaitForKeyPressAndGoToLobby()
    {
        // 죽자마자 바로 연타하다가 튕기는 걸 방지하기 위해 0.5초 정도 대기 정비 시간
        yield return new WaitForSeconds(0.5f);

        Debug.Log("💀 플레이어 패배: 아무 키나 누르면 로비로 이동합니다.");

        // 플레이어가 '아무 키'나 누를 때까지 무한 루프 돌며 대기
        while (true)
        {
            if ( Utils.IsAnyInputDown() )
            {
                break; // 루프 탈출
            }
            yield return null; // 다음 프레임까지 대기
        }

        // 3. 이동하기 전 마우스 락 완전히 해제 (이게 빠지면 로비 가서도 버튼 안 눌림)
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Destroy(gameObject);

        // 4. 로비 또는 캐릭터 선택창 씬으로 이동 (씬 이름은 프로젝트에 맞게 수정)
        SceneManager.LoadScene("Lobby");
        currentStage = 1;
    }
}

public static class Utils
{
    public static bool IsAnyInputDown()
    {
        if ( Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame )
            return true;

        if (Mouse.current != null)
        {
            if ( Mouse.current.leftButton.wasPressedThisFrame ||
                 Mouse.current.rightButton.wasPressedThisFrame ||
                 Mouse.current.middleButton.wasPressedThisFrame )
                return true;
        }

        return false;
    }
}
