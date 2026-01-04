using UnityEngine;
using PlayerSlot = LocalVersusGameManager.PlayerSlot;

[DisallowMultipleComponent]
public class LocalVersusRules : MonoBehaviour
{
    [SerializeField] private LocalVersusGameManager manager;

    private LocalVersusBindings bindings => manager.bindings;

    private GameObject _player1Instance => manager._player1Instance;
    private GameObject _player2Instance => manager._player2Instance;
    private GameObject _player3Instance => manager._player3Instance;
    private CharacterHealth _player1Health => manager._player1Health;
    private CharacterHealth _player2Health => manager._player2Health;
    private CharacterHealth _player3Health => manager._player3Health;

    private bool _respawnInProgress
    {
        get => manager._respawnInProgress;
        set => manager._respawnInProgress = value;
    }

    private bool _hunterIsPlayer1 => manager._hunterIsPlayer1;
    private GameUiManager player1Ui => manager.player1Ui;
    private GameUiManager player2Ui => manager.player2Ui;
    private GameUiManager player3Ui => manager.player3Ui;

    private ScoreboardController player1Scoreboard
    {
        get => manager.player1Scoreboard;
        set => manager.player1Scoreboard = value;
    }

    private ScoreboardController player2Scoreboard
    {
        get => manager.player2Scoreboard;
        set => manager.player2Scoreboard = value;
    }

    private ScoreboardController player3Scoreboard
    {
        get => manager.player3Scoreboard;
        set => manager.player3Scoreboard = value;
    }

    private int _player1Score
    {
        get => manager._player1Score;
        set => manager._player1Score = value;
    }

    private int _player2Score
    {
        get => manager._player2Score;
        set => manager._player2Score = value;
    }

    private int _player3Score
    {
        get => manager._player3Score;
        set => manager._player3Score = value;
    }

    private int scorePerTargetKill => manager != null && manager.gameplayTuning != null
        ? manager.gameplayTuning.scorePerTargetKill
        : (manager != null ? manager.scorePerTargetKill : 0);
    private int wrongTargetPenalty => manager != null && manager.gameplayTuning != null
        ? manager.gameplayTuning.wrongTargetPenalty
        : (manager != null ? -Mathf.Abs(manager.scorePerTargetKill) : 0);

    public void Initialize(LocalVersusGameManager manager)
    {
        this.manager = manager;
    }

    private void Awake()
    {
        if (!manager)
        {
            manager = GetComponent<LocalVersusGameManager>();
        }
    }

    private NpcIdentity GetIdentity(GameObject root)
    {
        return bindings ? bindings.GetIdentity(root) : null;
    }

    private AbilityRunner GetAbility(GameObject root)
    {
        return bindings ? bindings.GetAbility(root) : null;
    }

    internal void SubscribeHealth(CharacterHealth health)
    {
        if (!health)
        {
            return;
        }

        health.Died -= OnPlayerDied;
        health.Died += OnPlayerDied;
    }

    internal void UnsubscribeHealth(CharacterHealth health)
    {
        if (!health)
        {
            return;
        }

        health.Died -= OnPlayerDied;
    }

    private void OnPlayerDied(CharacterHealth dead)
    {
        if (_respawnInProgress || !dead)
        {
            return;
        }

        TryAwardScore(dead);
        ResetRevealCooldown(dead);
        if (manager.spawner)
        {
            StartCoroutine(manager.spawner.HandleRespawnAndSwap(dead));
        }
    }

    private void TryAwardScore(CharacterHealth dead)
    {
        if (!dead)
        {
            return;
        }

        if (!dead.TryGetLastDamage(out DamagePayload payload))
        {
            return;
        }

        CharacterHealth killerHealth = ResolveInstigatorHealth(payload);
        if (!killerHealth || killerHealth == dead)
        {
            return;
        }

        PlayerSlot? killerSlot = ResolvePlayerSlot(killerHealth);
        if (!killerSlot.HasValue)
        {
            return;
        }

        NpcIdentity deadIdentity = GetIdentity(dead.gameObject);
        if (!deadIdentity)
        {
            return;
        }

        NpcIdentity id1 = GetIdentity(_player1Instance);
        NpcIdentity id2 = GetIdentity(_player2Instance);
        NpcIdentity id3 = GetIdentity(_player3Instance);
        ResolveAssignedTargets(id1, id2, id3, out NpcIdentity target1, out NpcIdentity target2, out NpcIdentity target3);
        NpcIdentity assignedTarget = killerSlot.Value switch
        {
            PlayerSlot.Player1 => target1,
            PlayerSlot.Player2 => target2,
            PlayerSlot.Player3 => target3,
            _ => null
        };

        if (!assignedTarget)
        {
            return;
        }

        if (assignedTarget == deadIdentity)
        {
            if (scorePerTargetKill == 0)
            {
                return;
            }

            AddScore(killerSlot.Value, scorePerTargetKill);
            UpdateScoreboards();
            ShowKillPopup(killerHealth, scorePerTargetKill);
        }
        else
        {
            if (wrongTargetPenalty == 0)
            {
                return;
            }

            AddScore(killerSlot.Value, wrongTargetPenalty);
            UpdateScoreboards();
            ShowWrongTargetPopup(killerHealth, wrongTargetPenalty);
        }
    }

    internal void TryHandleHumiliation(CharacterHealth attacker, CharacterHealth victim)
    {
        if (!attacker || !victim || scorePerTargetKill <= 0)
        {
            return;
        }

        PlayerSlot? attackerSlot = ResolvePlayerSlot(attacker);
        PlayerSlot? victimSlot = ResolvePlayerSlot(victim);
        if (!attackerSlot.HasValue || !victimSlot.HasValue)
        {
            return;
        }

        NpcIdentity attackerId = GetIdentity(attacker.gameObject);
        if (!attackerId)
        {
            return;
        }

        NpcIdentity id1 = GetIdentity(_player1Instance);
        NpcIdentity id2 = GetIdentity(_player2Instance);
        NpcIdentity id3 = GetIdentity(_player3Instance);
        ResolveAssignedTargets(id1, id2, id3, out NpcIdentity target1, out NpcIdentity target2, out NpcIdentity target3);

        NpcIdentity victimTarget = victimSlot.Value switch
        {
            PlayerSlot.Player1 => target1,
            PlayerSlot.Player2 => target2,
            PlayerSlot.Player3 => target3,
            _ => null
        };

        if (victimTarget != attackerId)
        {
            return;
        }

        AddScore(attackerSlot.Value, scorePerTargetKill);
        UpdateScoreboards();
        ShowHumiliationPopup(attacker, scorePerTargetKill);
    }

    private void ShowKillPopup(CharacterHealth scorer, int points)
    {
        if (!scorer)
        {
            return;
        }

        PlayerFloatingTextController floatingText = GetFloatingTextController(scorer.gameObject);
        floatingText?.ShowKill(points);
    }

    private void ShowHumiliationPopup(CharacterHealth scorer, int points)
    {
        if (!scorer)
        {
            return;
        }

        PlayerFloatingTextController floatingText = GetFloatingTextController(scorer.gameObject);
        floatingText?.ShowHumiliation(points);
    }

    private void ShowWrongTargetPopup(CharacterHealth scorer, int points)
    {
        if (!scorer)
        {
            return;
        }

        PlayerFloatingTextController floatingText = GetFloatingTextController(scorer.gameObject);
        floatingText?.ShowWrongTarget(points);
    }

    internal void HandleWrongTargetKill(CharacterHealth dead)
    {
        if (!dead || wrongTargetPenalty == 0)
        {
            return;
        }

        if (!dead.TryGetLastDamage(out DamagePayload payload))
        {
            return;
        }

        CharacterHealth killerHealth = ResolveInstigatorHealth(payload);
        if (!killerHealth || killerHealth == dead)
        {
            return;
        }

        PlayerSlot? killerSlot = ResolvePlayerSlot(killerHealth);
        if (!killerSlot.HasValue)
        {
            return;
        }

        AddScore(killerSlot.Value, wrongTargetPenalty);
        UpdateScoreboards();
        ShowWrongTargetPopup(killerHealth, wrongTargetPenalty);
    }

    private PlayerFloatingTextController GetFloatingTextController(GameObject root)
    {
        return root ? root.GetComponent<PlayerFloatingTextController>() ?? root.GetComponentInChildren<PlayerFloatingTextController>(true) : null;
    }

    private CharacterHealth ResolveInstigatorHealth(DamagePayload payload)
    {
        GameObject instigator = payload.Instigator ? payload.Instigator : payload.Source;
        if (!instigator)
        {
            return null;
        }

        return instigator.GetComponent<CharacterHealth>()
            ?? instigator.GetComponentInParent<CharacterHealth>()
            ?? instigator.GetComponentInChildren<CharacterHealth>(true);
    }

    private PlayerSlot? ResolvePlayerSlot(CharacterHealth health)
    {
        if (!health)
        {
            return null;
        }

        if (health == _player1Health)
        {
            return PlayerSlot.Player1;
        }

        if (health == _player2Health)
        {
            return PlayerSlot.Player2;
        }

        if (health == _player3Health)
        {
            return PlayerSlot.Player3;
        }

        return null;
    }

    private void AddScore(PlayerSlot slot, int amount)
    {
        switch (slot)
        {
            case PlayerSlot.Player1:
                _player1Score += amount;
                break;
            case PlayerSlot.Player2:
                _player2Score += amount;
                break;
            case PlayerSlot.Player3:
                _player3Score += amount;
                break;
        }
    }

    private void ResetRevealCooldown(CharacterHealth dead)
    {
        AbilityRunner ability = null;
        if (dead == _player1Health)
        {
            ability = GetAbility(_player1Instance);
        }
        else if (dead == _player2Health)
        {
            ability = GetAbility(_player2Instance);
        }
        else if (dead == _player3Health)
        {
            ability = GetAbility(_player3Instance);
        }
        else
        {
            ability = dead.GetComponentInParent<AbilityRunner>() ?? dead.GetComponentInChildren<AbilityRunner>(true);
        }

        if (ability)
        {
            ability.ResetState();
        }
    }

    internal void ResolveAssignedTargets(NpcIdentity id1, NpcIdentity id2, NpcIdentity id3,
        out NpcIdentity target1, out NpcIdentity target2, out NpcIdentity target3)
    {
        bool hasPlayer3 = _player3Instance != null;
        target1 = id2;
        target2 = hasPlayer3 ? (id3 ? id3 : id1) : id1;
        target3 = hasPlayer3 ? id1 : null;
    }

    private void ResolveKillTargets(NpcIdentity id1, NpcIdentity id2, NpcIdentity id3,
        out NpcIdentity target1, out NpcIdentity target2, out NpcIdentity target3)
    {
        bool hasPlayer3 = _player3Instance != null;
        if (hasPlayer3)
        {
            target1 = id2;
            target2 = id3 ? id3 : id1;
            target3 = id1;
            return;
        }

        if (_hunterIsPlayer1)
        {
            target1 = id2;
            target2 = null;
        }
        else
        {
            target1 = null;
            target2 = id1;
        }
        target3 = null;
    }

    internal bool IsPlayerHealth(CharacterHealth health)
    {
        return health && (health == _player1Health || health == _player2Health || health == _player3Health);
    }

    internal bool CanKillPlayer(CharacterHealth attacker, CharacterHealth victim)
    {
        if (!attacker || !victim)
        {
            return true;
        }

        PlayerSlot? attackerSlot = ResolvePlayerSlot(attacker);
        PlayerSlot? victimSlot = ResolvePlayerSlot(victim);
        if (!attackerSlot.HasValue || !victimSlot.HasValue)
        {
            return true;
        }

        if (wrongTargetPenalty != 0)
        {
            return true;
        }

        NpcIdentity id1 = GetIdentity(_player1Instance);
        NpcIdentity id2 = GetIdentity(_player2Instance);
        NpcIdentity id3 = GetIdentity(_player3Instance);
        ResolveKillTargets(id1, id2, id3, out NpcIdentity target1, out NpcIdentity target2, out NpcIdentity target3);
        NpcIdentity victimId = GetIdentity(victim.gameObject);
        NpcIdentity allowedTarget = attackerSlot.Value switch
        {
            PlayerSlot.Player1 => target1,
            PlayerSlot.Player2 => target2,
            PlayerSlot.Player3 => target3,
            _ => null
        };

        return allowedTarget != null && allowedTarget == victimId;
    }

    internal void UpdateScoreboards()
    {
        ResolveScoreboards();
        bool updatedAny = false;
        updatedAny |= UpdateScoreboard(player1Scoreboard);
        updatedAny |= UpdateScoreboard(player2Scoreboard);
        updatedAny |= UpdateScoreboard(player3Scoreboard);

        if (updatedAny)
        {
            return;
        }

        ScoreboardController[] scoreboards = FindObjectsByType<ScoreboardController>(FindObjectsSortMode.None);
        if (scoreboards == null || scoreboards.Length == 0)
        {
            return;
        }

        for (int i = 0; i < scoreboards.Length; i++)
        {
            UpdateScoreboard(scoreboards[i]);
        }
    }

    private void ResolveScoreboards()
    {
        if (!player1Scoreboard && player1Ui)
        {
            player1Scoreboard = player1Ui.GetComponentInChildren<ScoreboardController>(true);
        }

        if (!player2Scoreboard && player2Ui)
        {
            player2Scoreboard = player2Ui.GetComponentInChildren<ScoreboardController>(true);
        }

        if (!player3Scoreboard && player3Ui)
        {
            player3Scoreboard = player3Ui.GetComponentInChildren<ScoreboardController>(true);
        }
    }

    private bool UpdateScoreboard(ScoreboardController scoreboard)
    {
        if (!scoreboard)
        {
            return false;
        }

        scoreboard.SetScores(_player1Score, _player2Score, _player3Score);
        return true;
    }

}
