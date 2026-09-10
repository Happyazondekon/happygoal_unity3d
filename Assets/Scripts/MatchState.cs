using System.Collections.Generic;

// Mirrors the still-needed surface of lib/models/game_state.dart's GameState:
// scores, shot history, sudden-death detection, and the single-slot rewind
// snapshot. Turn-sequencing/phase-transition logic that Dart needed only to
// drive reactive Flutter UI updates is NOT ported here - MatchController
// drives turns imperatively via coroutines instead (see its class doc), so
// there is no GamePhase enum on this side.
public class MatchState
{
    public int Team1Score;
    public int Team2Score;
    public int Team1Shots;
    public int Team2Shots;

    public readonly List<bool> Team1Results = new List<bool>();
    public readonly List<bool> Team2Results = new List<bool>();
    public readonly List<bool> Team1SuddenDeathResults = new List<bool>();
    public readonly List<bool> Team2SuddenDeathResults = new List<bool>();

    public readonly List<ShotData> Team1ShotData = new List<ShotData>();
    public readonly List<ShotData> Team2ShotData = new List<ShotData>();

    public bool IsSuddenDeathActive;

    // Rewind bookkeeping - MAX_REWINDS_PER_GAME-equivalent budget and the
    // single-slot snapshot GameState.saveStateBeforeShot()/rewindToLastShot()
    // used. Unlike Dart, the coin/ad economy behind how many rewinds are
    // available stays entirely in Flutter (see the plan's data contract) -
    // this is just spending a plain int handed down at launch.
    public int RewindsRemaining;
    public int RewindsUsed;
    MatchStateSnapshot _snapshot;

    public bool IsRegularPhase => Team1Shots < PenaltySettings.ShotsPerTeam || Team2Shots < PenaltySettings.ShotsPerTeam;

    public void RecordShotResult(bool isTeam1, bool isGoal, ShotData data)
    {
        if (isTeam1)
        {
            if (IsSuddenDeathActive) Team1SuddenDeathResults.Add(isGoal); else Team1Results.Add(isGoal);
            Team1Shots++;
            if (isGoal) Team1Score++;
            Team1ShotData.Add(data);
        }
        else
        {
            if (IsSuddenDeathActive) Team2SuddenDeathResults.Add(isGoal); else Team2Results.Add(isGoal);
            Team2Shots++;
            if (isGoal) Team2Score++;
            Team2ShotData.Add(data);
        }
    }

    // Port of GameState.checkWinner() - deliberately has the same side
    // effect (flips IsSuddenDeathActive on) as the Dart original, called at
    // the same point in the turn cycle (right after recording each shot).
    public bool CheckWinner()
    {
        if (IsRegularPhase)
        {
            int team1Remaining = PenaltySettings.ShotsPerTeam - Team1Shots;
            int team2Remaining = PenaltySettings.ShotsPerTeam - Team2Shots;
            if (Team1Score > Team2Score + team2Remaining) return true;
            if (Team2Score > Team1Score + team1Remaining) return true;
        }

        if (Team1Shots == PenaltySettings.ShotsPerTeam && Team2Shots == PenaltySettings.ShotsPerTeam)
        {
            if (Team1Score != Team2Score) return true;
            if (!IsSuddenDeathActive) IsSuddenDeathActive = true;
        }

        if (IsSuddenDeathActive)
        {
            int round = Team1SuddenDeathResults.Count;
            if (Team2SuddenDeathResults.Count == round && round > 0)
            {
                bool t1Last = Team1SuddenDeathResults[round - 1];
                bool t2Last = Team2SuddenDeathResults[round - 1];
                if (t1Last && !t2Last) return true;
                if (!t1Last && t2Last) return true;
            }
        }

        return false;
    }

    // Port of GameState.getWinner() - true = team1 won, false = team2 won,
    // null = draw/undetermined (shouldn't happen once CheckWinner() is true).
    public bool? IsTeam1Winner()
    {
        if (!IsSuddenDeathActive)
        {
            if (Team1Score > Team2Score) return true;
            if (Team2Score > Team1Score) return false;
            return null;
        }

        int lastRound = Team1SuddenDeathResults.Count - 1;
        if (lastRound >= 0 && Team2SuddenDeathResults.Count > lastRound)
        {
            bool t1 = Team1SuddenDeathResults[lastRound];
            bool t2 = Team2SuddenDeathResults[lastRound];
            if (t1 && !t2) return true;
            if (!t1 && t2) return false;
        }
        return null;
    }

    // ---- Rewind: port of GameState.saveStateBeforeShot()/rewindToLastShot()/
    // invalidateSnapshot() ----

    public void SaveStateBeforeShot()
    {
        _snapshot = new MatchStateSnapshot
        {
            team1Score = Team1Score,
            team2Score = Team2Score,
            team1Shots = Team1Shots,
            team2Shots = Team2Shots,
            isSuddenDeathActive = IsSuddenDeathActive,
            team1Results = new List<bool>(Team1Results),
            team2Results = new List<bool>(Team2Results),
            team1SuddenDeathResults = new List<bool>(Team1SuddenDeathResults),
            team2SuddenDeathResults = new List<bool>(Team2SuddenDeathResults),
            team1ShotData = new List<ShotData>(Team1ShotData),
            team2ShotData = new List<ShotData>(Team2ShotData),
        };
    }

    public bool CanRewind => _snapshot != null && RewindsRemaining > 0;

    public bool RewindToLastShot()
    {
        if (_snapshot == null) return false;

        Team1Score = _snapshot.team1Score;
        Team2Score = _snapshot.team2Score;
        Team1Shots = _snapshot.team1Shots;
        Team2Shots = _snapshot.team2Shots;
        IsSuddenDeathActive = _snapshot.isSuddenDeathActive;
        Team1Results.Clear(); Team1Results.AddRange(_snapshot.team1Results);
        Team2Results.Clear(); Team2Results.AddRange(_snapshot.team2Results);
        Team1SuddenDeathResults.Clear(); Team1SuddenDeathResults.AddRange(_snapshot.team1SuddenDeathResults);
        Team2SuddenDeathResults.Clear(); Team2SuddenDeathResults.AddRange(_snapshot.team2SuddenDeathResults);
        Team1ShotData.Clear(); Team1ShotData.AddRange(_snapshot.team1ShotData);
        Team2ShotData.Clear(); Team2ShotData.AddRange(_snapshot.team2ShotData);

        RewindsRemaining--;
        RewindsUsed++;
        InvalidateSnapshot();
        return true;
    }

    public void InvalidateSnapshot()
    {
        _snapshot = null;
    }

    class MatchStateSnapshot
    {
        public int team1Score, team2Score, team1Shots, team2Shots;
        public bool isSuddenDeathActive;
        public List<bool> team1Results, team2Results, team1SuddenDeathResults, team2SuddenDeathResults;
        public List<ShotData> team1ShotData, team2ShotData;
    }
}
