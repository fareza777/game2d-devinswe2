using System;
using System.Collections;
using System.IO;
using System.Linq;
using SmallScale.FantasyKingdomTileset;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Oathfire.Testing
{
    /// <summary>
    /// Plays Chapter 1 end to end without a human: boot, title, cinematic, prologue kit, then the Rennfall
    /// loop (gather, build, light the oathfire, survive the night). Every step asserts, so a broken chain
    /// fails loudly instead of looking fine in a screenshot. Enabled with -smoketest on the command line.
    /// </summary>
    public class ChapterOneSmokeTest : MonoBehaviour
    {
        const float StepTimeout = 90f;

        int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Environment.GetCommandLineArgs().Contains("-smoketest"))
                return;
            // A desktop player pauses when its window loses focus; a test run must not stall because someone
            // clicked another window while it played.
            Application.runInBackground = true;
            var go = new GameObject("ChapterOneSmokeTest");
            DontDestroyOnLoad(go);
            go.AddComponent<ChapterOneSmokeTest>();
        }

        IEnumerator Start()
        {
            Log("start");
            yield return WaitForScene(Core.GameBootstrap.TitleScene, "title screen");
            // Screenshots must wait for the fade curtain, or every capture is a black rectangle.
            yield return WaitForIdleFlow();
            yield return Capture("01_title");
            Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "exactly one audio listener");
            Check(Core.GameServices.Audio.CurrentMusic == "menu_theme", $"title music playing ({Core.GameServices.Audio.CurrentMusic})");

            UI.TitleScreen title = FindAnyObjectByType<UI.TitleScreen>();
            Check(title, "title screen present");
            if (title)
            {
                InvokeWith(title, "ToggleOptions", true);
                yield return new WaitForSeconds(0.5f);
                yield return Capture("01b_options");
                InvokeWith(title, "ToggleOptions", false);
            }
            yield return WaitForIdleFlow();
            InvokeMenu(title, "NewGame");

            yield return WaitForScene("Opening", "opening cinematic");
            yield return new WaitForSeconds(6f);
            yield return Capture("02_cinematic");
            Check(Core.GameServices.Audio.CurrentMusic == "opening_theme", $"cinematic music playing ({Core.GameServices.Audio.CurrentMusic})");
            float voiceDeadline = Time.time + 8f;
            while (!Core.GameServices.Audio.IsVoicePlaying && Time.time < voiceDeadline)
                yield return null;
            Check(Core.GameServices.Audio.IsVoicePlaying, "cinematic voice over playing");

            // The cinematic builds itself over a few frames; look for it until it appears rather than once.
            Cinematic.CinematicPlayer player = null;
            float cinematicDeadline = Time.time + 15f;
            while (!player && Time.time < cinematicDeadline)
            {
                player = FindAnyObjectByType<Cinematic.CinematicPlayer>();
                yield return null;
            }
            Check(player, "cinematic player present");
            Invoke(player, "skipRequested", true);

            yield return WaitForScene("Rennfall", "settlement");
            // The test drives the world itself; a visitor rolling in on their own mid-run would pre-empt the
            // scripted one later on.
            World.WorldEventDirector earlyEvents = FindAnyObjectByType<World.WorldEventDirector>();
            if (earlyEvents)
                earlyEvents.AutoEvents = false;
            yield return new WaitForSeconds(2f);

            Save.SaveData save = Core.GameServices.Save.Current;
            Check(save != null && save.HasFlag("prologue.done"), "prologue completed");
            Progress.PlayerState state = Progress.PlayerState.Instance;
            Check(state != null, "player state exists");
            Check(state != null && state.Inventory.Count("quest_testament") == 1, "testament granted");
            Check(state != null && state.Inventory.EquippedIn(Items.EquipSlot.MainHand) == "sword_guard_issue", "sword equipped");
            Check(PlayerHealth.Instance, "player spawned");
            ReportPlayerVisibility();
            yield return ProbeMovement("Rennfall");
            // The curve is deliberately slow now: the prologue banks experience, it does not hand out a level.
            Check(state != null && (state.Experience >= 150 || state.Level > 1),
                $"the prologue's reward is banked as experience (level {state?.Level}, {state?.Experience} xp)");
            Controls.MobileControls controlsForSkills = FindAnyObjectByType<Controls.MobileControls>();
            var areaSkill = controlsForSkills ? controlsForSkills.SlotButton(0) : null;
            int firstSkillLevel = Progress.SkillBook.Entries.Count > 0 ? Progress.SkillBook.Entries[0].level : 3;
            Check(Progress.SkillBook.Entries.Count >= 8, $"the skill book is filled ({Progress.SkillBook.Entries.Count} skills)");
            bool shouldHaveArea = state != null && state.Level >= firstSkillLevel;
            Check(areaSkill && areaSkill.gameObject.activeSelf == shouldHaveArea,
                $"the area skill is {(shouldHaveArea ? "learned" : "still locked")} at level {state?.Level}");
            Check(PlayerHealth.Instance && state != null && PlayerHealth.Instance.maxHealth == state.MaxHealth,
                $"the health bar follows the hero sheet ({(PlayerHealth.Instance ? PlayerHealth.Instance.maxHealth : 0)} vs {state?.MaxHealth})");
            Check(Core.GameServices.Audio.CurrentMusic == "village_day", $"village music playing ({Core.GameServices.Audio.CurrentMusic})");
            Check(Core.GameServices.Audio.CurrentAmbience == "village_day", $"village ambience playing ({Core.GameServices.Audio.CurrentAmbience})");
            UI.QuestCompass compass = FindAnyObjectByType<UI.QuestCompass>();
            Check(compass, "quest arrow present");
            yield return new WaitForSeconds(1.5f);
            Check(compass && compass.IsShowing, $"quest arrow guiding ({(compass ? compass.GuidedQuestId : "none")})");
            Quests.QuestDefinition shownQuest = Quests.QuestRuntime.ActiveMainQuest();
            Check(shownQuest != null && shownQuest.id != "q_prologue", $"HUD objective moved past the prologue ({shownQuest?.id})");
            Check(compass && shownQuest != null && compass.GuidedQuestId == shownQuest.id, "arrow guides the quest the HUD shows");
            yield return Capture("03_rennfall");
            yield return ProbeWall();
            yield return ProbeFirstConversation();
            yield return ProbeVillagersInView();
            yield return ProbeGatherLikeAPlayer();
            yield return ProbeEnterHouse();

            UI.HeroPanel book = FindAnyObjectByType<UI.HeroPanel>(FindObjectsInactive.Include);
            Check(book, "hero sheet present");
            if (book)
            {
                book.Toggle();
                yield return new WaitForSeconds(0.5f);
                Check(book.ShowingJournal && book.JournalEntryCount > 0, $"the book opens on the journal ({book.JournalEntryCount} lines)");
                int uninvited = Quests.QuestRuntime.Service.Quests.Count(quest => !quest.mainQuest && Quests.QuestRuntime.Service.IsAvailable(quest));
                Check(uninvited == 0, $"no side work is in the journal before anyone has asked ({uninvited} listed)");
                yield return Capture("03a_journal");
                book.ShowPage(journal: false);
                yield return new WaitForSeconds(0.4f);
                yield return Capture("03b_hero_sheet");
                book.Toggle();
            }

            UI.InventoryPanel pack = FindAnyObjectByType<UI.InventoryPanel>(FindObjectsInactive.Include);
            Check(pack, "pack present");
            if (pack)
            {
                pack.Toggle();
                yield return new WaitForSeconds(0.5f);
                Check(pack.ShowingEquipment, "the pack opens on the equipment plate");
                Check(pack.SlotCount == 10, $"the plate has a slot for every place gear is worn ({pack.SlotCount})");
                Check(state.Inventory.EquippedIn(Items.EquipSlot.Feet) == "boots_leather", "the Warden starts in road boots");
                yield return Capture("03c_equipment");
                pack.ShowPage(equipment: false);
                yield return new WaitForSeconds(0.5f);
                // This proves rows are created and given real height by the layout. Whether they are actually
                // drawn is what the screenshot below is for; a mask can hide rows that exist.
                Check(pack.RowCount > 0, $"pack lists items ({pack.RowCount} rows)");
                int everything = pack.RowCount;
                pack.ShowCategory(1);
                yield return null;
                int weaponsOnly = pack.RowCount;
                Check(weaponsOnly > 0 && weaponsOnly < everything, $"the bag filters by category ({everything} in all, {weaponsOnly} arms)");
                yield return Capture("03c_pack_arms");
                pack.ShowCategory(0);
                yield return null;
                Check(pack.FirstRowHeight > 1f, $"pack rows have height ({pack.FirstRowHeight:F0}px)");
                yield return Capture("03c_pack");
                // Open the card for the sword he starts with, the way a tap on its row would.
                Items.ItemDefinition sword = Items.ItemDatabase.Get("sword_guard_issue");
                InvokeWithArgs(pack, "OpenCard", sword, 1, true);
                yield return new WaitForSeconds(0.4f);
                yield return Capture("03d_item_card");
                pack.Toggle();
            }

            World.OathfireHearth hearth = World.OathfireHearth.Instance;
            Check(hearth, "hearth present");
            Check(FindObjectsByType<World.GatherNode>(FindObjectsSortMode.None).Length > 0, "gather nodes present");
            Check(FindObjectsByType<World.BuildSite>(FindObjectsSortMode.None).Length > 0, "build sites present");

            // Gathering: use every node once and confirm materials actually land in the bag.
            int timberBefore = state.Inventory.Count("mat_timber");
            foreach (World.GatherNode node in FindObjectsByType<World.GatherNode>(FindObjectsSortMode.None))
                node.GetComponent<World.WorldInteractable>().Use();
            yield return new WaitForSeconds(2f);
            Check(state.Inventory.Count("mat_timber") > timberBefore, $"gathering yields timber ({timberBefore} -> {state.Inventory.Count("mat_timber")})");

            // Building: give enough materials, raise a shelter, and confirm the safe radius grows.
            state.Inventory.Add("mat_timber", 20);
            state.Inventory.Add("mat_stone", 20);
            float radiusBefore = hearth.SafeRadius;
            hearth.GetComponent<World.WorldInteractable>().Use();
            yield return new WaitForSeconds(1.5f);
            Check(hearth.IsLit, "oathfire lit");
            Check(save.HasFlag("act1.oathfire_lit"), "oathfire flag set");

            World.BuildSite site = FindObjectsByType<World.BuildSite>(FindObjectsSortMode.None).First();
            site.GetComponent<World.WorldInteractable>().Use();
            yield return new WaitForSeconds(1f);
            Check(save.GetCounter("built.shelter") >= 1, "shelter built");
            Check(hearth.SafeRadius > radiusBefore, $"hearth light grew ({radiusBefore:F1} -> {hearth.SafeRadius:F1})");
            yield return Capture("04_hearth_lit");

            // Quests must have advanced from the same flags, with no manual bookkeeping.
            Quests.QuestDefinition quest = Quests.QuestRuntime.ActiveMainQuest();
            Check(quest != null, $"a main quest is active ({quest?.id})");

            // Night: the director spawns waves; the test kills them so the night can finish.
            int levelBeforeNight = state.Level;
            int experienceBeforeNight = state.Experience;
            save.SetFlag("act1.night1_ready");
            World.NightDirector night = World.NightDirector.Instance;
            Check(night, "night director present");
            night.BeginNight();
            yield return new WaitForSeconds(3f);
            Check(night.IsNightRunning, "night started");
            yield return new WaitForSeconds(2.5f);
            World.DayNightLighting sky = World.DayNightLighting.Instance;
            Check(sky && sky.Night > 0.9f, $"the world darkens for the night ({(sky ? sky.Night : 0f):F2})");
            Vector3 fire = World.OathfireHearth.Instance.transform.position;
            int unreachable = night.SpawnPoints.Count(point => !World.SpawnPlanner.IsOpen(point) || !World.SpawnPlanner.HasClearApproach(point, fire, World.NightDirector.SquareReach));
            Check(night.SpawnPoints.Count > 0 && unreachable == 0,
                $"the dead appear on open ground with a clear way to the fire ({night.SpawnPoints.Count} spawned, {unreachable} stuck)");
            // The first night is one plain kind of enemy; variety arrives on later nights.
            var firstNight = EnemyHealth2D.All.Where(enemy => enemy && !enemy.IsDead).ToArray();
            int oddOnes = firstNight.Count(enemy => !enemy.name.Contains("Warrior")
                || (enemy.GetComponentInChildren<SpriteRenderer>() && enemy.GetComponentInChildren<SpriteRenderer>().color != Color.white));
            Check(firstNight.Length > 0 && oddOnes == 0, $"the first night brings only plain skeleton warriors ({firstNight.Length} seen, {oddOnes} other)");
            int heaviest = EnemyHealth2D.All.Where(enemy => enemy && !enemy.IsDead).Select(enemy => enemy.maxHealth).DefaultIfEmpty(0).Max();
            Check(heaviest > 0 && heaviest <= 60, $"the first night's dead are weakened (toughest has {heaviest} health)");
            var sync = PlayerHealth.Instance ? PlayerHealth.Instance.GetComponent<World.PlayerDamageSync>() : null;
            var heroBlade = PlayerHealth.Instance ? PlayerHealth.Instance.GetComponent<PlayerMeleeHitbox>() : null;
            Check(sync && heroBlade && heroBlade.damage >= state.Damage, $"the sword hits as hard as the hero sheet says (sheet {state.Damage}, sword {(heroBlade ? heroBlade.damage : 0)})");
            yield return Capture("05_night");
            yield return ProbeSwordBites();

            float deadline = Time.time + StepTimeout * 2f;
            int killed = 0;
            while (night.IsNightRunning && Time.time < deadline)
            {
                if (PlayerHealth.Instance)
                    PlayerHealth.Instance.currentHealth = PlayerHealth.Instance.maxHealth;
                foreach (EnemyHealth2D enemy in EnemyHealth2D.All.ToArray())
                {
                    if (!enemy || enemy.IsDead)
                        continue;
                    enemy.TakeDamage(99999, Vector2.up);
                    killed++;
                }
                yield return null;
            }
            Check(!night.IsNightRunning, $"night finished (killed {killed})");
            Check(save.GetCounter("kills.night1") > 0, $"kills counted ({save.GetCounter("kills.night1")})");
            Check(Progress.KillRewards.LastPaid > 0 && (state.Level > levelBeforeNight || state.Experience > experienceBeforeNight),
                $"kills pay experience ({Progress.KillRewards.LastPaid} for the last; level {levelBeforeNight}->{state.Level}, xp {experienceBeforeNight}->{state.Experience})");
            Check(save.HasFlag("act1.night1_cleared"), "night flag set");
            Check(night.SpawnPoints.All(point => World.SpawnPlanner.IsOpen(point)), $"every wave spawned in the open ({night.SpawnPoints.Count} enemies)");
            yield return new WaitForSeconds(4.5f);
            Check(sky && sky.Night < 0.1f, $"daylight returns at dawn ({(sky ? sky.Night : 1f):F2})");
            yield return Capture("06_dawn");

            // A level gained must heal, raise the health bar's ceiling, and say so on screen.
            int levelBeforeGain = state.Level;
            PlayerHealth.Instance.currentHealth = Mathf.Max(1, PlayerHealth.Instance.maxHealth / 3);
            state.AddExperience(state.ExperienceForNextLevel - state.Experience);
            yield return null;
            yield return null;
            Check(state.Level == levelBeforeGain + 1, $"experience raises the level ({levelBeforeGain} -> {state.Level})");
            Check(PlayerHealth.Instance.maxHealth == state.MaxHealth && PlayerHealth.Instance.currentHealth == PlayerHealth.Instance.maxHealth,
                $"a new level heals to the new maximum ({PlayerHealth.Instance.currentHealth}/{PlayerHealth.Instance.maxHealth}, sheet {state.MaxHealth})");
            Check(UI.GameplayHud.Instance && UI.GameplayHud.Instance.ShowingLevelUp, "the level banner is shown");
            yield return new WaitForSeconds(0.5f);
            yield return Capture("06b_level_up");
            while (state.Level < firstSkillLevel)
                state.AddExperience(state.ExperienceForNextLevel);
            yield return new WaitForSeconds(0.3f);
            Check(areaSkill && areaSkill.gameObject.activeSelf, $"the first skill goes on the controls at level {firstSkillLevel} (level {state.Level})");

            // More levels open more skills; the second slot fills, and the book lets them be swapped.
            while (state.Level < 5)
                state.AddExperience(state.ExperienceForNextLevel);
            yield return new WaitForSeconds(0.8f);
            var secondSlot = controlsForSkills ? controlsForSkills.SlotButton(1) : null;
            Check(secondSlot && secondSlot.gameObject.activeSelf, $"a second skill fills the second slot by level 5 ({Progress.SkillBook.InSlot(1)?.id})");
            UI.HeroPanel skillBook = FindAnyObjectByType<UI.HeroPanel>(FindObjectsInactive.Include);
            if (skillBook)
            {
                skillBook.Toggle();
                skillBook.ShowSkills();
                yield return new WaitForSeconds(0.5f);
                Check(skillBook.ShowingSkills && skillBook.SkillRowCount >= 8, $"the book has a skills page ({skillBook.SkillRowCount} rows)");
                yield return Capture("06c_skills");
                Progress.SkillBook.Entry third = Progress.SkillBook.Entries.Count > 2 ? Progress.SkillBook.Entries[2] : null;
                if (third != null)
                {
                    Progress.SkillBook.Assign(0, third.id);
                    Check(Progress.SkillBook.InSlot(0) == third, $"a learned skill can be moved into slot I ({third.id})");
                }
                skillBook.Toggle();
            }

            // Falling in a fight must not end the game: the Warden wakes by the fire.
            int recoveriesBefore = World.DefeatRecovery.Recoveries;
            PlayerHealth.Instance.TakeDamage(99999, Vector2.up);
            yield return new WaitForSeconds(0.5f);
            Check(PlayerHealth.IsPlayerDead, "the hero can fall");
            float wakeDeadline = Time.time + 12f;
            while (PlayerHealth.IsPlayerDead && Time.time < wakeDeadline)
                yield return null;
            yield return WaitForIdleFlow();
            yield return new WaitForSeconds(1.5f);
            Check(!PlayerHealth.IsPlayerDead && World.DefeatRecovery.Recoveries == recoveriesBefore + 1
                && PlayerHealth.Instance.currentHealth == PlayerHealth.Instance.maxHealth, "a fallen hero wakes, whole, to fight again");
            yield return ProbeMovement("Rennfall after a fall");

            yield return ProbeQuestOffer();
            yield return ProbeShop(state);
            yield return ProbeRecordHouse(save, state);
            yield return ProbeSecondNight(save);

            // The notice board must post work, accept it, track it against real counters, and pay out.
            Contracts.ContractBoard.RollPostings(save.GetCounter("nights.survived"));
            Check(Contracts.ContractBoard.Posted.Count > 0, $"board posts contracts ({Contracts.ContractBoard.Posted.Count})");
            Contracts.PostedContract cull = Contracts.ContractBoard.Posted.FirstOrDefault(c => c.Template != null && c.Template.kind == Contracts.ContractKind.Cull);
            if (cull != null)
            {
                Contracts.ContractBoard.Accept(cull);
                Check(cull.accepted, $"contract accepted ({cull.templateId} x{cull.amount})");
                save.AddCounter(cull.Template.target, cull.amount);
                Check(Contracts.ContractBoard.IsComplete(cull), "contract reads as complete after kills");
                int coinBefore = state.Inventory.coin;
                Check(Contracts.ContractBoard.TryClaim(cull), "contract paid out");
                Check(state.Inventory.coin > coinBefore, $"coin increased ({coinBefore} -> {state.Inventory.coin})");
            }
            else
            {
                Check(false, "a cull contract was posted");
            }

            // A scripted visitor must arrive, be talkable, and pay out where the conversation lands.
            World.WorldEventDirector events = FindAnyObjectByType<World.WorldEventDirector>();
            Check(events, "world event director present");
            if (events)
            {
                // Stop the director rolling its own arrivals so this part of the run stays deterministic.
                events.AutoEvents = false;
                float idleDeadline = Time.time + StepTimeout * 2f;
                while (events.Running != null && Time.time < idleDeadline)
                    yield return null;
                Check(events.TryTrigger("ash_ledger"), "world event triggered (ash_ledger)");
                yield return new WaitForSeconds(1f);
                Check(events.Actor, "visitor spawned");
                Check(!string.IsNullOrEmpty(World.WorldEventDirector.Banner), "event banner shown");
                yield return Capture("07_visitor");

                // The reward lives in the conversation, so check the script actually carries it rather than
                // handing out coin here and calling that a pass.
                Dialogue.DialogueScript script = Dialogue.DialogueScript.Load("event_ash_ledger");
                Check(script != null, "event dialogue loads");
                Dialogue.DialogueNode payoff = script?.Node("rider_full_2");
                Check(payoff != null && payoff.giveCoin > 0 && payoff.giveItems.Length > 0, "event dialogue carries its reward");

                // Resolving is the dialogue's job; the test stands in for the player finishing it.
                save.SetFlag("event.ash_ledger.done");
                float eventDeadline = Time.time + StepTimeout;
                while (events.Running != null && Time.time < eventDeadline)
                    yield return null;
                Check(events.Running == null, "event resolved and visitor left");
                Check(string.IsNullOrEmpty(World.WorldEventDirector.Banner), "banner cleared after the event");
            }

            // Saving must round-trip the run.
            Check(Core.GameServices.Save.Autosave("smoke", "Rennfall"), "autosave written");
            Check(Core.GameServices.Save.Peek(Save.SaveService.AutosaveSlot) != null, "autosave readable");

            // Greymarch: the city must load, put the player in it, and be worth looking at.
            Core.GameServices.Flow.LoadScene("Greymarch");
            yield return WaitForScene("Greymarch", "Greymarch");
            yield return new WaitForSeconds(3f);
            Check(PlayerHealth.Instance, "player spawned in Greymarch");
            ReportPlayerVisibility();
            yield return ProbeMovement("Greymarch");
            yield return ProbeCityGate();
            Check(Core.GameServices.Audio.CurrentMusic == "city_greymarch", $"city music playing ({Core.GameServices.Audio.CurrentMusic})");
            yield return WaitForIdleFlow();
            yield return Capture("08_greymarch");
            yield return ProbeHollow(save, state);
            ProbeEverySideQuest(save, state);

            string verdict = failures == 0 ? "PASS" : $"FAIL ({failures} checks failed)";
            Log($"RESULT {verdict}");
            // Written last, so its presence proves this run finished rather than the window being closed
            // part way through. Reading a log alone cannot tell a finished run from an abandoned one.
            File.WriteAllText(
                Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "smoke_result.txt"),
                $"{verdict}\n{System.DateTime.Now:O}\n");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        /// <summary>
        /// Act II's first beat end to end: Corporal Dace points the way, the trail opens off the trade road, the
        /// Hollow's two fights and its captain fall, the carter is freed, and the ledger reaches Vesna.
        /// </summary>
        IEnumerator ProbeHollow(Save.SaveData save, Progress.PlayerState state)
        {
            save.SetFlag("act2.summons_done");
            save.SetFlag("act2.bandits_hinted");
            Quests.QuestRuntime.Evaluate();
            Quests.QuestDefinition hollow = Quests.QuestRuntime.Service.Quests.FirstOrDefault(quest => quest.id == "q_hollow");
            Check(hollow != null && Quests.QuestRuntime.Service.IsAvailable(hollow), "the Hollow quest opens after the summons");
            Check(hollow != null && Quests.QuestRuntime.Service.CurrentStage(hollow)?.id == "s1", "it starts with Corporal Dace");

            World.NpcDialogue dace = FindObjectsByType<World.NpcDialogue>(FindObjectsSortMode.None).FirstOrDefault(npc => npc.SpeakerKey == "speaker.dace");
            var daceTalk = CallPrivate(dace, "PickConversation") as World.NpcDialogue.Conversation;
            Check(daceTalk != null && daceTalk.scriptId == "city_dace_raids", $"Dace briefs the Warden on the raids ({daceTalk?.scriptId})");
            int talkers = FindObjectsByType<World.NpcDialogue>(FindObjectsSortMode.None).Length;
            Check(talkers >= 7, $"Greymarch's market is full of people ({talkers} to talk to)");

            yield return PlayConversation("city_dace_raids");
            Check(save.HasFlag("act2.dace_briefed"), "the briefing marks the trail");

            // Leaving the city: the hero must arrive at the Greymarch end of the road, with the trail now open.
            Core.GameServices.Flow.LoadScene("TradeRoad");
            yield return WaitForScene("TradeRoad", "the trade road");
            yield return WaitForIdleFlow();
            yield return new WaitForSeconds(1.5f);
            Check(World.PlayerArrival.ArrivedBy == "Greymarch", $"the hero arrives by the Greymarch road ({World.PlayerArrival.ArrivedBy})");
            World.SceneExit trail = FindObjectsByType<World.SceneExit>(FindObjectsSortMode.None).FirstOrDefault(exit => exit.Destination == "Hollow");
            Check(trail, "the trail into the Hollow is open");
            Check(Quests.QuestGuide.TryFind(PlayerHealth.Instance.transform.position, out Quests.QuestGuide.Waypoint way) && way.quest != null,
                "the quest arrow has somewhere to point on the road");
            if (!trail)
                yield break;
            Teleport(trail.transform.position + new Vector3(0.6f, -0.3f, 0f));
            yield return new WaitForSeconds(0.8f);
            yield return Capture("09a_trail");
            trail.GetComponent<World.WorldInteractable>().Use();

            yield return WaitForScene("Hollow", "Blackthorn Hollow");
            yield return WaitForIdleFlow();
            yield return new WaitForSeconds(1.2f);
            Check(save.HasFlag("act2.found_hollow"), "taking the trail is remembered");
            Check(UI.GameplayHud.Instance && UI.GameplayHud.Instance.ShowingPlaceName, "the Hollow announces its name");
            yield return Capture("09b_hollow_arrival");
            Check(PlayerHealth.Instance, "player spawned in the Hollow");
            ReportPlayerVisibility();
            yield return ProbeMovement("Hollow");

            World.RoadAmbush[] fights = FindObjectsByType<World.RoadAmbush>(FindObjectsSortMode.None)
                .OrderBy(fight => fight.ClearedFlag == "act2.hollow_camp_cleared" ? 0 : 1).ToArray();
            Check(fights.Length == 2, $"the Hollow has its camp and its pass ({fights.Length})");
            int levelBefore = state.Level, experienceBefore = state.Experience;
            foreach (World.RoadAmbush fight in fights)
            {
                Teleport(fight.transform.position + new Vector3(0.4f, 0.2f, 0f));
                float springDeadline = Time.time + 6f;
                while (!fight.IsFighting && Time.time < springDeadline)
                    yield return null;
                yield return new WaitForSeconds(1.2f);
                Check(fight.IsFighting && fight.AliveCount > 0, $"{fight.name} springs ({fight.AliveCount} raiders)");
                // A narrow pass packs raiders tighter than a lone spawn: measured at a raider's crowd size, not the roomy spawn size.
                int stuck = fight.SpawnPoints.Count(point => !World.SpawnPlanner.IsOpen(point, 0.28f));
                foreach (Vector3 point in fight.SpawnPoints)
                    Log($"{fight.name} spawn {point} open={World.SpawnPlanner.IsOpen(point)} crowdOpen={World.SpawnPlanner.IsOpen(point, 0.28f)}");
                int stackedPairs = fight.SpawnPoints.SelectMany((a, i) => fight.SpawnPoints.Skip(i + 1).Select(b => Vector2.Distance(a, b))).Count(d => d < 0.5f);
                Check(stackedPairs == 0, $"{fight.name}: raiders do not appear stacked on one spot ({stackedPairs} pairs too close)");
                Check(fight.SpawnPoints.Count > 0 && stuck == 0, $"{fight.name}: every raider appears on open ground ({stuck} of {fight.SpawnPoints.Count} in a wall)");
                yield return Capture(fight.ClearedFlag == "act2.hollow_camp_cleared" ? "10a_hollow_camp" : "10b_hollow_pass");
                yield return KillEverything(8f);
                yield return new WaitForSeconds(1.5f);
                Check(save.HasFlag(fight.ClearedFlag), $"{fight.name} is cleared ({fight.ClearedFlag})");
            }
            Check(save.GetCounter("kills.hollow") >= 7, $"the camp's kills count toward the quest ({save.GetCounter("kills.hollow")}/7)");
            Check(state.Level > levelBefore || state.Experience > experienceBefore, "the Hollow's fights pay experience");

            World.BossEncounter rook = FindAnyObjectByType<World.BossEncounter>();
            Check(rook, "Rook Varr waits in the ruin");
            if (rook)
            {
                Teleport(rook.transform.position + new Vector3(1.4f, -0.5f, 0f));
                float talkDeadline = Time.time + 6f;
                while (!Dialogue.DialogueRunner.IsConversationActive && Time.time < talkDeadline)
                    yield return null;
                Check(rook.Current == World.BossEncounter.Phase.Talking && Dialogue.DialogueRunner.IsConversationActive, "Rook talks before he fights");
                yield return new WaitForSeconds(1f);
                yield return Capture("11a_rook_talks");
                yield return FinishConversation();
                yield return new WaitForSeconds(1f);
                Check(rook.Current == World.BossEncounter.Phase.Fighting && rook.Fighter, "the talking stops and the fight starts");
                Check(!string.IsNullOrEmpty(World.WorldEventDirector.Banner), $"his name and health are shown ({World.WorldEventDirector.Banner})");
                Check(rook.Fighter && World.SpawnPlanner.IsOpen(rook.Fighter.transform.position), "Rook stands on open ground");
                yield return new WaitForSeconds(1f);
                yield return Capture("11b_rook_fight");
                int beforeBoss = Progress.KillRewards.LastPaid;
                yield return KillEverything(8f);
                yield return new WaitForSeconds(1.5f);
                Check(save.HasFlag("act2.rook_defeated"), "Rook's fall is recorded");
                Check(Progress.KillRewards.LastPaid > beforeBoss, $"a captain pays more than his crew ({beforeBoss} -> {Progress.KillRewards.LastPaid})");
            }

            World.NpcDialogue mira = FindObjectsByType<World.NpcDialogue>(FindObjectsSortMode.None).FirstOrDefault(npc => npc.SpeakerKey == "speaker.mira");
            Check(mira && mira.GetComponentInChildren<SpriteRenderer>()?.sprite, "Mira Holt is there, and drawn");
            var miraTalk = CallPrivate(mira, "PickConversation") as World.NpcDialogue.Conversation;
            Check(miraTalk?.scriptId == "hollow_mira_freed", $"with Rook down, Mira can be freed ({miraTalk?.scriptId})");
            if (mira)
                Teleport(mira.transform.position + new Vector3(1f, -0.4f, 0f));
            yield return new WaitForSeconds(0.5f);
            yield return PlayConversation("hollow_mira_freed");
            Check(save.HasFlag("act2.prisoner_freed") && state.Inventory.Count("quest_harrow_ledger") == 1, "Mira is free and the ledger is in the pack");
            yield return Capture("12_hollow_done");

            // Back to the city: Mira is there now, and the ledger closes the quest at Vesna's stall.
            Core.GameServices.Flow.LoadScene("Greymarch");
            yield return WaitForScene("Greymarch", "Greymarch again");
            yield return WaitForIdleFlow();
            yield return new WaitForSeconds(1.5f);
            Check(FindObjectsByType<World.NpcDialogue>(FindObjectsSortMode.None).Any(npc => npc.SpeakerKey == "speaker.mira"), "Mira has come to Greymarch");
            World.NpcDialogue vesna = FindObjectsByType<World.NpcDialogue>(FindObjectsSortMode.None).FirstOrDefault(npc => npc.SpeakerKey == "speaker.vesna");
            var vesnaTalk = CallPrivate(vesna, "PickConversation") as World.NpcDialogue.Conversation;
            Check(vesnaTalk?.scriptId == "vesna_ledger", $"Vesna wants to see the ledger ({vesnaTalk?.scriptId})");
            int coinBefore = state.Inventory.coin;
            yield return PlayConversation("vesna_ledger");
            Quests.QuestRuntime.Evaluate();
            Check(save.HasFlag("act2.ledger_delivered") && save.HasFlag("act2.keep_rumoured"), "the ledger is delivered and the iron keep named");
            Check(hollow != null && Quests.QuestRuntime.Service.IsComplete(hollow), "Blackthorn Hollow is complete");
            Check(state.Inventory.coin > coinBefore, $"the quest pays ({coinBefore} -> {state.Inventory.coin})");
            yield return Capture("13_ledger_delivered");
        }

        /// <summary>
        /// Stands the hero next to one of the night's dead and swings. The complaint this answers is that an
        /// enemy's health never seemed to move: it checks the number falls and that a bar is there to show it.
        /// </summary>
        IEnumerator ProbeSwordBites()
        {
            EnemyHealth2D target = EnemyHealth2D.All.FirstOrDefault(enemy => enemy && !enemy.IsDead);
            Check(target, "there is something to fight");
            if (!target)
                yield break;
            Check(target.GetComponent<World.EnemyVitals>(), "the enemy wears a health bar");

            int before = target.CurrentHealth;
            Teleport(target.transform.position + new Vector3(0.55f, -0.15f, 0f));
            yield return new WaitForSeconds(0.4f);
            Controls.MobileControls controls = FindAnyObjectByType<Controls.MobileControls>();
            if (controls)
                controls.enabled = false;
            InputAdapter.VirtualControlsActive = true;
            Camera view = Camera.main;
            for (int swing = 0; swing < 6 && target && !target.IsDead && target.CurrentHealth == before; swing++)
            {
                if (view)
                    InputAdapter.VirtualPointerScreen = view.WorldToScreenPoint(target.transform.position);
                InputAdapter.SetVirtualKey(KeyCode.Mouse0, true);
                yield return new WaitForSeconds(0.12f);
                InputAdapter.SetVirtualKey(KeyCode.Mouse0, false);
                yield return new WaitForSeconds(0.55f);
            }
            if (controls)
                controls.enabled = true;
            int after = target ? target.CurrentHealth : 0;
            Check(!target || target.IsDead || after < before, $"the sword takes health off ({before} -> {after} of {(target ? target.MaxHealth : 0)})");
            Log($"sword probe: hero damage {Progress.PlayerState.Instance?.Damage}, enemy {before} -> {after}");
            yield return Capture("05b_sword");
        }

        /// <summary>
        /// Side work has to be offered by somebody and taken before it counts. This talks to the villager who
        /// has work, accepts it off the card, and checks it lands in the journal.
        /// </summary>
        IEnumerator ProbeQuestOffer()
        {
            Quests.QuestService service = Quests.QuestRuntime.Service;
            World.NpcDialogue giver = FindObjectsByType<World.NpcDialogue>(FindObjectsSortMode.None)
                .FirstOrDefault(npc => npc.PendingOffer() != null);
            Check(giver, $"somebody in the valley has work to offer ({(giver ? giver.SpeakerKey : "nobody")})");
            if (!giver)
                yield break;
            Quests.QuestDefinition offered = giver.PendingOffer();
            Check(giver.GetComponentInChildren<World.QuestOffers>(), "the villager is marked as having work");
            Check(!service.IsAvailable(offered), $"{offered.id} is not in the journal before it is taken");

            Teleport(giver.transform.position + new Vector3(1.1f, -0.4f, 0f));
            yield return new WaitForSeconds(0.4f);
            InvokeMenu(giver, "Talk");
            yield return new WaitForSeconds(1f);
            yield return FinishConversation();
            yield return new WaitForSeconds(0.8f);
            UI.QuestOfferPanel card = UI.QuestOfferPanel.Instance;
            Check(card && card.Showing == offered, $"the work is put to the Warden as a card ({card?.Showing?.id})");
            yield return Capture("07b_quest_offer");

            Button accept = card ? card.GetComponentsInChildren<Button>(true).FirstOrDefault(button => button.name == "Accept") : null;
            Check(accept, "the card has a way to take the work on");
            if (accept)
                accept.onClick.Invoke();
            yield return new WaitForSeconds(0.5f);
            Check(service.IsAvailable(offered) && service.CurrentStage(offered) != null, $"{offered.id} is now the Warden's to do");
            Check(Quests.QuestRuntime.FocusedQuest() == offered, $"the new work becomes what the arrow follows ({Quests.QuestRuntime.FocusedQuest()?.id})");
            Quests.QuestRuntime.Track(offered);
            Check(Quests.QuestRuntime.TrackedQuest() == null && Quests.QuestRuntime.FocusedQuest()?.mainQuest == true,
                "tapping it again hands the arrow back to the story");
            Quests.QuestRuntime.Track(offered);
            Check(Controls.MobileControls.GameplayActive, "the controls come back after the card closes");
        }

        /// <summary>The front gate of Greymarch has to be a way in, not a wall with a door painted on it.</summary>
        IEnumerator ProbeCityGate()
        {
            World.SceneExit road = FindObjectsByType<World.SceneExit>(FindObjectsSortMode.None)
                .FirstOrDefault(exit => exit.Destination == "TradeRoad");
            World.NoticeBoard board = FindAnyObjectByType<World.NoticeBoard>();
            if (!road || !board)
                yield break;

            // From the road outside, walk the way the gate faces and see whether the city is entered.
            Teleport(road.transform.position);
            yield return new WaitForSeconds(0.5f);
            float before = Vector2.Distance(PlayerHealth.Instance.transform.position, board.transform.position);
            Controls.MobileControls controls = FindAnyObjectByType<Controls.MobileControls>();
            if (controls)
                controls.enabled = false;
            InputAdapter.VirtualControlsActive = true;
            Vector2 towards = ((Vector2)(board.transform.position - PlayerHealth.Instance.transform.position)).normalized;
            for (float t = 0f; t < 6f; t += Time.deltaTime)
            {
                InputAdapter.VirtualMove = ((Vector2)(board.transform.position - PlayerHealth.Instance.transform.position)).normalized;
                yield return null;
            }
            InputAdapter.VirtualMove = Vector2.zero;
            if (controls)
                controls.enabled = true;
            float after = Vector2.Distance(PlayerHealth.Instance.transform.position, board.transform.position);
            Log($"gate probe: {before:F1} -> {after:F1} units from the city notices (heading {towards})");
            Check(after < before - 3f, $"the hero walks in through the city gate ({before:F1} -> {after:F1} units to the notices)");
            yield return Capture("08b_gate");
        }

        /// <summary>Opens Sabel's stall, buys something plain, sells something, and checks nothing rare is for sale.</summary>
        IEnumerator ProbeShop(Progress.PlayerState state)
        {
            UI.ShopPanel shop = UI.ShopPanel.Instance;
            Check(shop, "a merchant's stall exists");
            if (!shop)
                yield break;
            shop.Open("sabel");
            yield return new WaitForSeconds(0.4f);
            Check(UI.ShopPanel.IsOpen && shop.RowCount > 5, $"Sabel's stall opens with wares ({shop.RowCount})");
            Check(!shop.Stocks(item => item.rarity >= Items.ItemRarity.Rare), "nothing rare is for sale: good gear is earned");
            yield return Capture("07c_shop");
            state.Inventory.coin += 60;
            int coin = state.Inventory.coin;
            int potions = state.Inventory.Count("potion_small");
            Check(shop.BuyById("potion_small") && state.Inventory.Count("potion_small") == potions + 1 && state.Inventory.coin < coin,
                $"buying takes coin and gives the item ({coin} -> {state.Inventory.coin})");
            shop.ShowPage(sell: true);
            yield return new WaitForSeconds(0.3f);
            coin = state.Inventory.coin;
            bool sold = shop.Sell(Items.ItemDatabase.Get("potion_small"), shop.SellPrice(Items.ItemDatabase.Get("potion_small")));
            Check(sold && state.Inventory.coin > coin && shop.SellPrice(Items.ItemDatabase.Get("potion_small")) < 30,
                $"selling pays, and less than the price to buy ({coin} -> {state.Inventory.coin})");
            yield return Capture("07d_shop_sell");
            shop.Close();
            Check(!UI.ShopPanel.IsOpen && Controls.MobileControls.GameplayActive, "leaving the stall gives the controls back");
        }

        /// <summary>
        /// Gethin's work end to end in the world: take it, search the ruin, deal with what wakes, bring the
        /// journal back, and get paid from the hand-in card.
        /// </summary>
        IEnumerator ProbeRecordHouse(Save.SaveData save, Progress.PlayerState state)
        {
            Quests.QuestService service = Quests.QuestRuntime.Service;
            Quests.QuestDefinition records = service.Quests.FirstOrDefault(quest => quest.id == "q_records_house");
            if (records == null)
                yield break;
            save.SetFlag(records.unlockFlag);
            service.Accept(records);
            yield return new WaitForSeconds(0.8f);
            World.SearchSpot spot = FindObjectsByType<World.SearchSpot>(FindObjectsSortMode.None).FirstOrDefault(s => s.FoundFlag == "act1.found_crane_journal");
            Check(spot, "the ruin has somewhere to search once the work is taken");
            if (!spot)
                yield break;
            save.trackedQuest = records.id;
            Check(Quests.QuestGuide.TryFind(PlayerHealth.Instance.transform.position, out Quests.QuestGuide.Waypoint way) && way.quest == records
                  && Vector2.Distance(way.position, spot.transform.position) < 0.5f, "the arrow leads to the search");
            Teleport(spot.transform.position + new Vector3(0.7f, -0.3f, 0f));
            yield return new WaitForSeconds(0.6f);
            int before = EnemyHealth2D.All.Count(enemy => enemy && !enemy.IsDead);
            spot.GetComponent<World.WorldInteractable>().Use();
            yield return new WaitForSeconds(1f);
            int woke = EnemyHealth2D.All.Count(enemy => enemy && !enemy.IsDead) - before;
            Check(state.Inventory.Count("quest_crane_journal") > 0, "searching finds Crane's journal");
            Check(woke > 0, $"the search wakes what guarded it ({woke})");
            yield return Capture("07e_search");
            yield return KillEverything(8f);
            yield return new WaitForSeconds(0.8f);
            Check(!spot || !spot.gameObject.activeInHierarchy, "the searched spot is gone");
            Check(service.IsReadyToHandIn(records), "the work waits to be handed back");
            Check(Quests.QuestRuntime.StepText(records).Contains(Core.GameServices.Localization.Get("speaker.gethin")),
                $"the step says who to go back to ({Quests.QuestRuntime.StepText(records)})");

            World.NpcDialogue gethin = FindObjectsByType<World.NpcDialogue>(FindObjectsSortMode.None).FirstOrDefault(npc => npc.SpeakerKey == "speaker.gethin");
            Check(gethin && gethin.WaitingForHandIn(), "Gethin is waiting for the journal");
            if (!gethin)
                yield break;
            Teleport(gethin.transform.position + new Vector3(1f, -0.4f, 0f));
            yield return new WaitForSeconds(0.4f);
            int experience = state.Experience + state.Level * 100000;
            InvokeMenu(gethin, "Talk");
            yield return new WaitForSeconds(0.8f);
            UI.QuestOfferPanel card = UI.QuestOfferPanel.Instance;
            Check(card && card.Showing == records, "talking to him brings up the hand-in card");
            yield return Capture("07f_hand_in");
            Button handIn = card ? card.GetComponentsInChildren<Button>(true).FirstOrDefault(button => button.name == "Accept") : null;
            if (handIn)
                handIn.onClick.Invoke();
            yield return new WaitForSeconds(0.6f);
            Check(service.IsComplete(records), "the record house work is complete");
            Check(state.Inventory.Count("quest_crane_journal") == 0, "the journal changed hands");
            Check(state.Experience + state.Level * 100000 > experience, "handing it in pays");
            yield return FinishConversation();
            if (UI.QuestOfferPanel.Instance && UI.QuestOfferPanel.Instance.Showing != null)
                UI.QuestOfferPanel.Instance.GetComponentsInChildren<Button>(true).FirstOrDefault(button => button.name == "Later")?.onClick.Invoke();
        }

        /// <summary>After the first night, the fire must be able to call another: kills and later enemy kinds come from nights.</summary>
        IEnumerator ProbeSecondNight(Save.SaveData save)
        {
            World.NightDirector night = World.NightDirector.Instance;
            World.OathfireHearth hearth = World.OathfireHearth.Instance;
            if (!night || !hearth)
                yield break;
            float deadline = Time.time + World.NightDirector.RestSeconds + 10f;
            while (!night.CanCallAnotherNight && Time.time < deadline)
                yield return null;
            Check(night.CanCallAnotherNight, "the fire can call another night once the valley has rested");
            int nights = save.GetCounter("nights.survived");
            Teleport(hearth.transform.position + new Vector3(1.2f, -0.5f, 0f));
            yield return new WaitForSeconds(0.4f);
            hearth.GetComponent<World.WorldInteractable>().Use();
            yield return new WaitForSeconds(1.5f);
            Check(night.IsNightRunning, "tending the fire calls the second night");
            float end = Time.time + StepTimeout;
            while (night.IsNightRunning && Time.time < end)
            {
                if (PlayerHealth.Instance)
                    PlayerHealth.Instance.currentHealth = PlayerHealth.Instance.maxHealth;
                foreach (EnemyHealth2D enemy in EnemyHealth2D.All.ToArray())
                    if (enemy && !enemy.IsDead)
                        enemy.TakeDamage(99999, Vector2.up);
                yield return new WaitForSeconds(0.3f);
            }
            Check(save.GetCounter("nights.survived") == nights + 1, $"the second night counts ({nights} -> {save.GetCounter("nights.survived")})");
            Check(save.GetCounter("kills.mage") > 0, $"the second night brings the chanting dead ({save.GetCounter("kills.mage")} mages)");
        }

        /// <summary>
        /// Every piece of side work, taken, done and handed back. The world probes above prove the mechanics; this
        /// proves no quest hangs on a stage nothing can satisfy and that each one pays when it is handed in.
        /// </summary>
        void ProbeEverySideQuest(Save.SaveData save, Progress.PlayerState state)
        {
            Quests.QuestService service = Quests.QuestRuntime.Service;
            int finished = 0, total = 0;
            foreach (Quests.QuestDefinition quest in service.Quests.Where(q => q.HasGiver).ToList())
            {
                if (service.IsComplete(quest))
                {
                    finished++;
                    total++;
                    continue;
                }
                total++;
                if (!string.IsNullOrEmpty(quest.unlockFlag))
                    save.SetFlag(quest.unlockFlag);
                service.Accept(quest);
                foreach (Quests.QuestStage stage in quest.stages)
                {
                    foreach (Quests.QuestObjective objective in stage.objectives)
                    {
                        switch (objective.kind)
                        {
                            case Quests.ObjectiveKind.Flag: save.SetFlag(objective.key); break;
                            case Quests.ObjectiveKind.Counter:
                                save.AddCounter(objective.key, Mathf.Max(0, objective.amount - save.GetCounter(objective.key)));
                                break;
                            case Quests.ObjectiveKind.Item:
                                state.Inventory.Add(objective.key, Mathf.Max(0, objective.amount - state.Inventory.Count(objective.key)));
                                break;
                        }
                    }
                    service.Evaluate();
                }
                bool ready = service.IsReadyToHandIn(quest);
                bool handed = ready && service.HandIn(quest);
                bool done = service.IsComplete(quest);
                if (done)
                    finished++;
                else
                    Log($"side quest stuck: {quest.id} ready={ready} handed={handed} stage={service.CurrentStage(quest)?.id}");
            }
            Check(finished == total, $"every side quest can be taken, done and handed back ({finished}/{total})");
        }

        IEnumerator PlayConversation(string scriptId)
        {
            Dialogue.DialogueRunner.Instance.Play(scriptId);
            yield return new WaitForSeconds(0.6f);
            Check(Dialogue.DialogueRunner.IsConversationActive, $"{scriptId} plays");
            yield return FinishConversation();
        }

        /// <summary>Taps through a conversation, taking the first choice whenever one is offered.</summary>
        IEnumerator FinishConversation()
        {
            Dialogue.DialoguePanel panel = FindAnyObjectByType<Dialogue.DialoguePanel>();
            float deadline = Time.time + 60f;
            while (panel && Dialogue.DialogueRunner.IsConversationActive && Time.time < deadline)
            {
                Button choice = panel.VisibleChoices.Count > 0
                    ? panel.GetComponentsInChildren<Button>().FirstOrDefault(button => button.gameObject.activeInHierarchy)
                    : null;
                if (choice)
                    choice.onClick.Invoke();
                else
                    panel.OnPointerClick(null);
                yield return new WaitForSeconds(0.35f);
            }
            Check(!Dialogue.DialogueRunner.IsConversationActive, "the conversation finishes");
        }

        IEnumerator KillEverything(float seconds)
        {
            float deadline = Time.time + seconds;
            do
            {
                if (PlayerHealth.Instance)
                    PlayerHealth.Instance.currentHealth = PlayerHealth.Instance.maxHealth;
                foreach (EnemyHealth2D enemy in EnemyHealth2D.All.ToArray())
                    if (enemy && !enemy.IsDead)
                        enemy.TakeDamage(99999, Vector2.up);
                yield return new WaitForSeconds(0.3f);
            }
            while (Time.time < deadline && EnemyHealth2D.All.Any(enemy => enemy && !enemy.IsDead));
        }

        static void Teleport(Vector3 position)
        {
            if (!PlayerHealth.Instance)
                return;
            PlayerHealth.Instance.transform.position = position;
            var body = PlayerHealth.Instance.GetComponent<Rigidbody2D>();
            if (body)
                body.position = position;
        }

        static object GetField(object target, string field) =>
            target?.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(target);

        static object CallPrivate(object target, string method) =>
            target?.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(target, null);

        /// <summary>
        /// Says where the hero actually is and whether anything is drawing them. "The player is invisible"
        /// has three unrelated causes — no sprite, drawn behind the world, or off camera — and a screenshot
        /// cannot tell them apart.
        /// </summary>
        static void ReportPlayerVisibility()
        {
            if (!PlayerHealth.Instance)
                return;
            Transform player = PlayerHealth.Instance.transform;
            var renderer = player.GetComponentInChildren<SpriteRenderer>();
            Camera camera = Camera.main;

            Vector3 viewport = camera ? camera.WorldToViewportPoint(player.position) : Vector3.zero;
            bool onScreen = viewport.x is >= 0f and <= 1f && viewport.y is >= 0f and <= 1f;
            Log($"player at {player.position} | camera at {camera?.transform.position} | " +
                $"viewport {viewport.x:F2},{viewport.y:F2} onScreen={onScreen}");
            Log(renderer
                ? $"sprite '{renderer.sprite?.name}' enabled={renderer.enabled} alpha={renderer.color.a:F2} " +
                  $"order={renderer.sortingOrder} visible={renderer.isVisible} scale={player.localScale}"
                : "player has no SpriteRenderer at all");
        }

        /// <summary>
        /// Pushes the virtual joystick in four directions from wherever the hero stands and measures how far
        /// they actually went. A hero stuck in a wall reads zero in every direction.
        /// </summary>
        IEnumerator ProbeMovement(string where)
        {
            Controls.MobileControls controls = FindAnyObjectByType<Controls.MobileControls>();
            if (controls)
                controls.enabled = false;
            InputAdapter.VirtualControlsActive = true;
            Transform hero = PlayerHealth.Instance ? PlayerHealth.Instance.transform : null;
            int moved = 0;
            float total = 0f;
            foreach (Vector2 direction in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left })
            {
                if (!hero)
                    break;
                Vector3 start = hero.position;
                InputAdapter.VirtualMove = direction;
                yield return new WaitForSeconds(0.6f);
                InputAdapter.VirtualMove = Vector2.zero;
                yield return new WaitForSeconds(0.15f);
                float distance = Vector2.Distance(start, hero.position);
                total += distance;
                if (distance > 0.3f)
                    moved++;
                Log($"move probe {where} {direction}: {distance:F2}");
            }
            if (controls)
                controls.enabled = true;
            Check(moved >= 3, $"hero walks from spawn in {where} ({moved}/4 directions, {total:F1} units)");
        }

        /// <summary>
        /// Walks the hero into a real stretch of wall and checks the feet never overlap it. A flat wall is one
        /// that also blocks the neighbouring directions; a lone tile corner lets the hero slide round it, which
        /// is correct and would read as "walked through" if measured only by distance.
        /// </summary>
        IEnumerator ProbeWall()
        {
            PlayerHealth hero = PlayerHealth.Instance;
            var feet = hero ? hero.GetComponent<CircleCollider2D>() : null;
            var mover = hero ? hero.GetComponent<World.WallSlideMover>() : null;
            Check(feet && mover, "hero has feet and the wall mover");
            if (!feet || !mover)
                yield break;

            int worldMask = LayerMask.GetMask("World");
            Vector2 heading = Vector2.zero;
            float wallDistance = float.MaxValue;
            float Hit(Vector2 from, float degrees)
            {
                var direction = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
                RaycastHit2D hit = Physics2D.CircleCast(from, feet.bounds.extents.x, direction, 5f, worldMask);
                return hit && hit.distance > 0.05f ? hit.distance : float.MaxValue;
            }
            // Look from a few spots around the spawn, so a wall is found even in an open square.
            Vector2 origin = feet.bounds.center;
            for (int step = 0; step < 24; step++)
            {
                float degrees = step * 15f;
                float here = Hit(origin, degrees);
                bool flat = Hit(origin, degrees - 15f) < here * 1.6f + 0.3f && Hit(origin, degrees + 15f) < here * 1.6f + 0.3f;
                if (flat && here < wallDistance)
                {
                    wallDistance = here;
                    heading = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
                }
            }
            Check(heading != Vector2.zero, "wall probe found a stretch of wall near the hero");
            if (heading == Vector2.zero)
                yield break;

            Controls.MobileControls controls = FindAnyObjectByType<Controls.MobileControls>();
            if (controls)
                controls.enabled = false;
            InputAdapter.VirtualControlsActive = true;
            Vector2 start = feet.bounds.center;
            int overlapFrames = 0;
            int clippedSteps = 0;
            float deepest = 0f;
            InputAdapter.VirtualMove = heading;
            for (float t = 0f; t < wallDistance / 1.6f + 1.5f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                Collider2D inside = Physics2D.OverlapCircle(feet.bounds.center, feet.bounds.extents.x * 0.8f, worldMask);
                if (inside)
                {
                    overlapFrames++;
                    ColliderDistance2D gap = Physics2D.Distance(feet, inside);
                    deepest = Mathf.Min(deepest, gap.distance);
                }
                if (mover.LastWanted.sqrMagnitude > 0f && mover.LastAllowed.magnitude < mover.LastWanted.magnitude * 0.9f)
                    clippedSteps++;
            }
            InputAdapter.VirtualMove = Vector2.zero;
            yield return new WaitForSeconds(0.2f);
            if (controls)
                controls.enabled = true;

            float along = Vector2.Dot((Vector2)feet.bounds.center - start, heading);
            Log($"wall probe: wall {wallDistance:F2} away along {heading}, went {along:F2}; mover clipped {clippedSteps} steps; feet overlapped {overlapFrames} frames, deepest {deepest:F2}");
            Check(clippedSteps > 0, $"the wall mover engages at a wall ({clippedSteps} clipped steps)");
            Check(overlapFrames == 0, $"walls stop the hero (feet overlapped {overlapFrames} frames, deepest {deepest:F2}, went {along:F2} toward a wall {wallDistance:F2} away)");
        }

        /// <summary>
        /// Talks to Brann the way a player would: taps through his lines, then picks a choice by raycasting the
        /// screen exactly where a thumb would land. A choice that cannot be hit leaves the conversation stuck.
        /// </summary>
        IEnumerator ProbeFirstConversation()
        {
            World.NpcDialogue brann = FindObjectsByType<World.NpcDialogue>(FindObjectsSortMode.None)
                .FirstOrDefault(npc => npc.SpeakerKey == "speaker.brann");
            Dialogue.DialoguePanel panel = FindAnyObjectByType<Dialogue.DialoguePanel>();
            Check(brann && panel, "Brann and the dialogue panel exist");
            if (!brann || !panel)
                yield break;

            SpriteRenderer body = brann.GetComponentInChildren<SpriteRenderer>();
            Check(body && body.enabled && body.sprite, $"Brann has a visible body ({(body && body.sprite ? body.sprite.name : "none")})");
            int bodiless = FindObjectsByType<World.NpcDialogue>(FindObjectsSortMode.None)
                .Count(npc => !npc.GetComponentInChildren<SpriteRenderer>()?.sprite);
            Check(bodiless == 0, $"every villager is drawn ({bodiless} without a body)");
            PlayerHealth.Instance.transform.position = brann.transform.position + new Vector3(1.2f, -0.4f, 0f);
            yield return new WaitForSeconds(0.5f);
            InvokeMenu(brann, "Talk");
            yield return new WaitForSeconds(1.5f);
            Check(Dialogue.DialogueRunner.IsConversationActive, "first conversation started");
            yield return Capture("03e_dialogue");

            float deadline = Time.time + 60f;
            while (panel.VisibleChoices.Count == 0 && Dialogue.DialogueRunner.IsConversationActive && Time.time < deadline)
            {
                panel.OnPointerClick(null);
                yield return new WaitForSeconds(0.4f);
            }
            Check(panel.VisibleChoices.Count > 0, $"conversation reached a choice ({panel.VisibleChoices.Count} shown)");
            yield return new WaitForSeconds(0.3f);
            yield return Capture("03f_dialogue_choices");

            Button first = panel.GetComponentsInChildren<Button>().FirstOrDefault(button => button.gameObject.activeInHierarchy);
            GameObject tapped = null;
            if (first)
            {
                Vector2 thumb = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)first.transform).position);
                var pointer = new PointerEventData(EventSystem.current) { position = thumb };
                var results = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, results);
                tapped = results.Count > 0 ? results[0].gameObject : null;
                Check(tapped && tapped.GetComponentInParent<Button>() == first, $"a tap on a choice lands on it ({(tapped ? tapped.name : "nothing")})");
                if (tapped)
                    ExecuteEvents.ExecuteHierarchy(tapped, pointer, ExecuteEvents.pointerClickHandler);
            }

            deadline = Time.time + 60f;
            while (Dialogue.DialogueRunner.IsConversationActive && Time.time < deadline)
            {
                panel.OnPointerClick(null);
                yield return new WaitForSeconds(0.4f);
            }
            Check(!Dialogue.DialogueRunner.IsConversationActive, "conversation finishes after picking a choice");
            Check(Controls.MobileControls.GameplayActive, "controls return after the conversation");
        }

        /// <summary>No villager may stand where a roof covers them: a roof tile sits in front on screen.</summary>
        IEnumerator ProbeVillagersInView()
        {
            UnityEngine.Tilemaps.Tilemap ground = GameObject.Find("Ground")?.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            var roofs = FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None).Where(map => map.name.StartsWith("Roof")).ToArray();
            int hidden = 0;
            foreach (World.NpcDialogue npc in FindObjectsByType<World.NpcDialogue>(FindObjectsSortMode.None))
            {
                if (!ground)
                    break;
                Vector3Int cell = ground.WorldToCell(npc.transform.position);
                bool covered = false;
                for (int back = 1; back <= 6 && !covered; back++)
                    for (int side = -2; side <= 2 && !covered; side++)
                    {
                        int d = cell.x + cell.y - back, c = cell.x - cell.y + side;
                        if ((d + c) % 2 != 0)
                            continue;
                        var front = new Vector3Int((d + c) / 2, (d - c) / 2, 0);
                        covered = roofs.Any(map => map.HasTile(front));
                    }
                if (covered)
                {
                    hidden++;
                    Log($"villager under a roof: {npc.name} at {cell}");
                }
            }
            Check(ground && hidden == 0, $"no villager stands hidden under a roof ({hidden})");
            yield break;
        }

        /// <summary>Walks up to a tree, waits for the Use button to offer it, taps the button, then strikes the tree.</summary>
        IEnumerator ProbeGatherLikeAPlayer()
        {
            Progress.PlayerState state = Progress.PlayerState.Instance;
            World.GatherNode tree = FindObjectsByType<World.GatherNode>(FindObjectsSortMode.None)
                .Where(node => node.ItemId == "mat_timber" && node.IsAvailable)
                .OrderBy(node => (node.transform.position - PlayerHealth.Instance.transform.position).sqrMagnitude)
                .FirstOrDefault();
            Check(tree && state != null, "a timber tree to gather");
            if (!tree || state == null)
                yield break;

            PlayerHealth.Instance.transform.position = tree.transform.position + new Vector3(0.7f, -0.35f, 0f);
            yield return new WaitForSeconds(0.6f);
            World.WorldInteractable focus = World.WorldInteractable.Focused;
            Check(focus && focus.gameObject == tree.gameObject, $"standing at the tree focuses it ({(focus ? focus.name : "nothing")})");
            GameObject use = GameObject.Find("UseButton");
            Check(use && use.activeInHierarchy, "the Use button appears beside the tree");
            yield return Capture("03g_gather_prompt");

            int before = state.Inventory.Count("mat_timber");
            if (use)
                yield return TapLikeAThumb(use);
            yield return new WaitForSeconds(2f);
            int afterUse = state.Inventory.Count("mat_timber");
            Check(afterUse > before, $"tapping Use gathers timber ({before} -> {afterUse})");

            // Striking a tree must also chop it, and never break it for good.
            World.GatherNode other = FindObjectsByType<World.GatherNode>(FindObjectsSortMode.None)
                .FirstOrDefault(node => node.ItemId == "mat_timber" && node.IsAvailable && node != tree);
            var prop = other ? other.GetComponentInChildren<DestructibleProp2D>() : null;
            if (prop)
            {
                int beforeStrike = state.Inventory.Count("mat_timber");
                for (int i = 0; i < 6; i++)
                {
                    prop.ApplyHit(1);
                    yield return new WaitForSeconds(0.15f);
                }
                yield return new WaitForSeconds(2f);
                Check(state.Inventory.Count("mat_timber") > beforeStrike, $"striking a tree chops timber ({beforeStrike} -> {state.Inventory.Count("mat_timber")})");
                Check(other.GetComponentsInChildren<Collider2D>().Any(collider => collider.isTrigger && collider.enabled)
                      && other.GetComponent<SpriteRenderer>() && other.GetComponent<SpriteRenderer>().enabled,
                    "a struck tree still stands and can be used");
            }
        }

        /// <summary>Opens a house door with the Use button, walks in, and checks the roof lifts.</summary>
        IEnumerator ProbeEnterHouse()
        {
            World.HouseDoor[] doors = FindObjectsByType<World.HouseDoor>(FindObjectsSortMode.None);
            Check(doors.Length > 0, $"the village has enterable houses ({doors.Length})");
            World.HouseDoor door = doors.FirstOrDefault();
            UnityEngine.Tilemaps.Tilemap ground = GameObject.Find("Ground")?.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            if (!door || !ground)
                yield break;

            var feet = PlayerHealth.Instance.GetComponent<CircleCollider2D>();
            Vector3 feetOffset = feet.bounds.center - PlayerHealth.Instance.transform.position;
            Vector3 outside = ground.GetCellCenterWorld(door.Outside);
            Vector3 inside = ground.GetCellCenterWorld(door.Inside);
            PlayerHealth.Instance.transform.position = outside - feetOffset;
            yield return new WaitForSeconds(0.6f);
            World.WorldInteractable focus = World.WorldInteractable.Focused;
            Check(focus && focus.gameObject == door.gameObject, $"standing at the door focuses it ({(focus ? focus.name : "nothing")})");
            GameObject use = GameObject.Find("UseButton");
            if (use && use.activeInHierarchy)
                yield return TapLikeAThumb(use);
            yield return new WaitForSeconds(0.3f);
            Check(door.IsOpen, "tapping Use opens the door");

            Controls.MobileControls controls = FindAnyObjectByType<Controls.MobileControls>();
            if (controls)
                controls.enabled = false;
            InputAdapter.VirtualControlsActive = true;
            InputAdapter.VirtualMove = ((Vector2)(inside - outside)).normalized;
            yield return new WaitForSeconds(1.4f);
            InputAdapter.VirtualMove = Vector2.zero;
            if (controls)
                controls.enabled = true;
            yield return new WaitForSeconds(1f);

            Vector3Int standing = ground.WorldToCell(feet.bounds.center);
            float intoHouse = Vector2.Dot((Vector2)(feet.bounds.center - outside), ((Vector2)(inside - outside)).normalized);
            Log($"house probe: door {door.Cell}, hero now at {standing}, {intoHouse:F2} units in");
            Check(intoHouse > 0.9f, $"the hero walks through the open door ({intoHouse:F2} units in)");
            World.RoofFader fader = World.RoofFader.Instance;
            Check(fader && fader.LowestAlpha < 0.5f, $"the roof lifts while the hero is inside ({(fader ? fader.LowestAlpha : 1f):F2})");
            yield return Capture("03h_inside_house");
            PlayerHealth.Instance.transform.position = outside - feetOffset;
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>Presses and releases a control at its centre through the event system, as a touch would.</summary>
        IEnumerator TapLikeAThumb(GameObject control)
        {
            Vector2 thumb = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)control.transform).position);
            var pointer = new PointerEventData(EventSystem.current) { position = thumb };
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);
            GameObject hit = results.Count > 0 ? results[0].gameObject : null;
            Check(hit && hit.transform.IsChildOf(control.transform), $"a tap on {control.name} lands on it ({(hit ? hit.name : "nothing")})");
            if (!hit)
                yield break;
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            yield return new WaitForSeconds(0.08f);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerUpHandler);
        }

        static IEnumerator WaitForIdleFlow()
        {
            while (Core.GameServices.Flow && Core.GameServices.Flow.IsBusy)
                yield return null;
        }

        IEnumerator WaitForScene(string sceneName, string label)
        {
            float deadline = Time.time + StepTimeout;
            while (SceneManager.GetActiveScene().name != sceneName && Time.time < deadline)
                yield return null;
            Check(SceneManager.GetActiveScene().name == sceneName, $"reached {label}");
        }

        void Check(bool condition, string what)
        {
            if (!condition)
                failures++;
            Log($"{(condition ? "ok  " : "FAIL")} {what}");
        }

        static void InvokeMenu(object target, string method) =>
            target?.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(target, null);

        static void InvokeWithArgs(object target, string method, params object[] arguments) =>
            target?.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(target, arguments);

        static void InvokeWith(object target, string method, object argument) =>
            target?.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(target, new[] { argument });

        static void Invoke(object target, string field, object value) =>
            target?.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(target, value);

        IEnumerator Capture(string name)
        {
            if (Application.isBatchMode)
                yield break;
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", $"smoke_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Log($"screenshot {path}");
        }

        static void Log(string message) => Debug.Log($"SMOKE {message}");
    }
}
