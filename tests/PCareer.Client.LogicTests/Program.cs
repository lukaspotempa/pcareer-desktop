using PCareer.Client;
using PCareer.Client.Models;
using PCareer.Client.Services;
using System.Text.Json;
using PCareer.Client.LogicTests;

Assert(
    PCareer.Client.Program.SelectServerUrl(
        "http://localhost:8000/",
        allowDevelopmentOverride: false)
        == PCareer.Client.Program.ProductionServerUrl,
    "Production builds must ignore a development server environment override.");
Assert(
    PCareer.Client.Program.SelectServerUrl(
        "http://localhost:8000/",
        allowDevelopmentOverride: true)
        == "http://localhost:8000/",
    "Development builds should allow an explicit local server override.");
Assert(
    PCareer.Client.Program.SelectServerUrl(" ", allowDevelopmentOverride: true)
        == PCareer.Client.Program.ProductionServerUrl,
    "An empty development override should fall back to the production server.");

var sessionStoreDirectory = Path.Combine(
    Path.GetTempPath(),
    $"vpn-session-store-{Guid.NewGuid():N}");
var sessionStorePath = Path.Combine(sessionStoreDirectory, "session.dat");
var sessionStore = new DesktopSessionStore(sessionStorePath);
var now = DateTimeOffset.UtcNow;
var desktopSession = new DesktopSession
{
    AccessToken = "access-token",
    AccessExpiresAt = now.AddMinutes(15),
    RefreshToken = "refresh-token",
    RefreshExpiresAt = now.AddDays(10),
    User = new AuthenticatedUser(1, "discord-id", "pilot", "Test Pilot", null),
};
try
{
    var persistenceDeadline = sessionStore.Save(desktopSession);
    Assert(
        persistenceDeadline <= now.AddDays(7).AddSeconds(1),
        "Desktop sessions must never persist for longer than seven days.");
    var restoredSession = sessionStore.Load();
    Assert(
        restoredSession?.Session.RefreshToken == desktopSession.RefreshToken,
        "The encrypted desktop session should be restorable for the current Windows user.");

    sessionStore.Save(desktopSession, now.AddSeconds(-1));
    Assert(
        sessionStore.Load() is null && !File.Exists(sessionStorePath),
        "Expired persisted sessions must be removed instead of restored.");
}
finally
{
    sessionStore.Clear();
    if (Directory.Exists(sessionStoreDirectory))
    {
        Directory.Delete(sessionStoreDirectory, recursive: true);
    }
}

Assert(
    PortableUpdater.ParseVersion("v1.2.3") == new Version(1, 2, 3),
    "Portable update versions should accept the release tag format.");
var validManifest = new PortableUpdateManifest(
    "1.2.3",
    "https://github.com/lukaspotempa/pcareer-desktop/releases/download/v1.2.3/VirtualPilotNetwork.exe",
    new string('a', 64),
    1024);
validManifest.Validate();
Assert(
    validManifest.ParsedVersion > new Version(1, 2, 2),
    "A newer portable release should compare above the installed version.");
AssertThrows<InvalidDataException>(
    () => new PortableUpdateManifest(
        "1.2.3",
        "http://example.com/VirtualPilotNetwork.exe",
        new string('a', 64),
        1024).Validate(),
    "Portable updates must reject non-HTTPS downloads.");
AssertThrows<InvalidDataException>(
    () => new PortableUpdateManifest(
        "1.2.3",
        "https://example.com/VirtualPilotNetwork.exe",
        "not-a-hash",
        1024).Validate(),
    "Portable updates must reject invalid checksums.");

var controller = new FlightSessionController();
var contract = ContractAssignment.DevelopmentFlight;
var onGround = Sample(onGround: true, altitudeAgl: 5);
var normalizedForServer = PCareerApiClient.NormalizeTelemetryForServer(onGround);
var serverDerivedPayloadKg =
    (normalizedForServer.TotalWeightPounds - normalizedForServer.EmptyWeightPounds)
    * 0.45359237
    - normalizedForServer.FuelTotalKg;
Assert(
    Math.Abs(serverDerivedPayloadKg - onGround.PayloadWeightKg) < 0.001,
    "Legacy servers must derive the same payload total reported by the payload stations.");

Assert(
    controller.EvaluateReadiness(true, contract, onGround) == "Ready to begin loading.",
    "An on-ground 1x telemetry sample should be ready.");

var c172Contract = new ContractAssignment(
    ContractId: "C172-TEST",
    DepartureName: "Munich",
    ArrivalName: "Nuremberg",
    RequiredAircraftTitleContains: "Cessna 172 Skyhawk",
    DepartureLatitudeDegrees: null,
    DepartureLongitudeDegrees: null,
    DepartureRadiusNauticalMiles: 2)
{
    AircraftIcao = "C172",
    AircraftSimulatorIdentities = new[]
    {
        new AircraftSimulatorIdentity("msfs_2024", "atc_model", "prefix", "C172"),
        new AircraftSimulatorIdentity("msfs_2024", "title", "contains", "C172SP"),
    },
    AirlineIcao = "pcx",
    FlightNumber = "4821",
};
Assert(
    c172Contract.FlightDesignator == "PCX4821",
    "The public flight designator should not expose the internal contract ID.");
Assert(
    controller.EvaluateReadiness(
        true,
        c172Contract,
        Sample(
            onGround: true,
            altitudeAgl: 5,
            aircraftTitle: "C172SP G1000 Passengers",
            aircraftAtcModel: "C172"))
        == "Ready to begin loading.",
    "A configurable C172 passenger variant should match the required Cessna 172 Skyhawk.");
Assert(
    controller.EvaluateReadiness(
        true,
        c172Contract,
        Sample(
            onGround: true,
            altitudeAgl: 5,
            aircraftTitle: "Beechcraft Baron G58",
            aircraftAtcModel: "BE58"))
        == "Select the required aircraft: Cessna 172 Skyhawk.",
    "An unrelated aircraft must not pass the normalized aircraft check.");

var a320NeoContract = new ContractAssignment(
    ContractId: "A20N-TEST",
    DepartureName: "Munich",
    ArrivalName: "Frankfurt",
    RequiredAircraftTitleContains: "Airbus A-320neo",
    DepartureLatitudeDegrees: null,
    DepartureLongitudeDegrees: null,
    DepartureRadiusNauticalMiles: 2)
{
    AircraftIcao = "A20N",
    AircraftSimulatorIdentities = new[]
    {
        new AircraftSimulatorIdentity("msfs_2024", "atc_model", "exact", "A20N"),
        new AircraftSimulatorIdentity(
            "msfs_2024",
            "title",
            "contains",
            "Airbus A320 Neo FlyByWire"),
    },
};
Assert(
    controller.EvaluateReadiness(
        true,
        a320NeoContract,
        Sample(
            onGround: true,
            altitudeAgl: 5,
            aircraftTitle: "FWB SA Lufthansa D-AIJA",
            aircraftAtcModel: "A320",
            aircraftAtcType: "Airbus"))
        == "Ready to begin loading.",
    "A FlyByWire A320neo livery should match without depending on its airline title.");
Assert(
    SimulatorAircraftIdentity.DecodeAtcModel("ATCCOM.AC_MODEL_A20N.0.text") == "A20N"
        && SimulatorAircraftIdentity.DecodeAtcType("ATCCOM.ATC_NAME AIRBUS.0.text") == "AIRBUS",
    "Localized MSFS ATC resource keys should decode to stable aircraft identifiers.");
Assert(
    controller.EvaluateReadiness(
        true,
        a320NeoContract,
        Sample(
            onGround: true,
            altitudeAgl: 5,
            aircraftTitle: "FWB SA Lufthansa D-AIJA",
            aircraftAtcModel: "ATCCOM.AC_MODEL_A20N.0.text",
            aircraftAtcType: "ATCCOM.ATC_NAME AIRBUS.0.text"))
        == "Ready to begin loading.",
    "Localized MSFS identity keys should still match an A20N contract.");
var fbwPayloadPlan = FlyByWireA32NxLoadPlanner.CreatePayloadPlan(19_218.71);
Assert(
    fbwPayloadPlan.PassengersByZone.Sum() <= FlyByWireA32NxLoadPlanner.MaximumPassengers
        && Math.Abs(
            fbwPayloadPlan.PassengersByZone.Sum()
                * FlyByWireA32NxLoadPlanner.PassengerWeightKilograms
                + fbwPayloadPlan.CargoKilogramsByHold.Sum()
                - 19_218.71) < 0.01,
    "The FlyByWire payload plan should preserve the requested total mass.");
var fbwFuelPlan = FlyByWireA32NxLoadPlanner.CreateFuelPlan(5_000, 6.7);
Assert(
    Math.Abs(
        fbwFuelPlan.CenterGallons
            + fbwFuelPlan.LeftInnerGallons
            + fbwFuelPlan.LeftOuterGallons
            + fbwFuelPlan.RightInnerGallons
            + fbwFuelPlan.RightOuterGallons
            - fbwFuelPlan.TotalGallons) < 0.01,
    "The FlyByWire fuel plan should preserve the requested total volume.");
Assert(
    controller.EvaluateReadiness(
        true,
        a320NeoContract,
        Sample(
            onGround: true,
            altitudeAgl: 5,
            aircraftTitle: "FenixA320 CFM SL Lufthansa",
            aircraftAtcModel: "A320",
            aircraftAtcType: "Airbus"))
        == "Select the required aircraft: Airbus A-320neo.",
    "A different Airbus A320 family must not match the FlyByWire A320neo fallback.");

controller.BeginLoading();
Assert(controller.Phase == FlightPhase.Loading, "Start must first enter the loading phase.");
Assert(
    !controller.LoadingStatus(contract, onGround).Contains("±", StringComparison.Ordinal),
    "Player-facing readiness text must not reveal the load tolerance.");
var toleranceContract = contract with
{
    RequiredFuelKg = 100,
    RequiredPayloadKg = 200,
};
Assert(
    controller.LoadsMatch(
        toleranceContract,
        onGround with
        {
            FuelTotalKg = toleranceContract.RequiredFuelKg!.Value * 0.971,
            PayloadStationWeightPounds =
                toleranceContract.RequiredPayloadKg * 1.029 / 0.45359237,
        }),
    "Fuel and payload deviations within three percent must be accepted.");
Assert(
    !controller.LoadsMatch(
        toleranceContract,
        onGround with
        {
            FuelTotalKg = toleranceContract.RequiredFuelKg!.Value * 0.969,
            PayloadStationWeightPounds =
                toleranceContract.RequiredPayloadKg / 0.45359237,
        }),
    "A load deviation above three percent must still be rejected.");
Assert(
    controller.LoadsMatch(
        toleranceContract,
        onGround with
        {
            FuelTotalKg = toleranceContract.RequiredFuelKg!.Value,
            PayloadStationWeightPounds =
                toleranceContract.RequiredPayloadKg * 3.0 / 0.45359237,
        },
        ignorePayload: true),
    "Payload deviations beyond the tolerance must be accepted when payload is ignored (medevac).");
Assert(
    !controller.LoadsMatch(
        toleranceContract,
        onGround with
        {
            FuelTotalKg = toleranceContract.RequiredFuelKg!.Value,
            PayloadStationWeightPounds =
                toleranceContract.RequiredPayloadKg * 3.0 / 0.45359237,
        }),
    "The same payload deviation must still be rejected when payload is enforced.");
controller.Start(Guid.NewGuid(), onGround);
Assert(controller.Phase == FlightPhase.Started, "Start must enter Started.");

controller.Observe(Sample(onGround: false, altitudeAgl: 800));
Assert(controller.Phase == FlightPhase.Airborne, "Leaving the ground must enter Airborne.");

var reducedSimRateResult = controller.Observe(
    Sample(onGround: false, altitudeAgl: 800) with { SimulationRate = 0.5 });
Assert(reducedSimRateResult is null, "Reducing the simulation rate must not cancel the flight.");
Assert(controller.SimRateIncreased, "A reduced simulation rate must remain tracked for bonus eligibility.");

var simRateResult = controller.Observe(
    Sample(onGround: false, altitudeAgl: 800) with { SimulationRate = 2 });
Assert(simRateResult is null, "Increasing the simulation rate must not cancel the flight.");
Assert(controller.SimRateIncreased, "A simulation-rate increase must remain tracked.");

controller.Observe(Sample(onGround: true, altitudeAgl: 8) with
{
    TouchdownNormalVelocityFeetPerSecond = -5,
    GForce = 1.4,
});
Assert(controller.Phase == FlightPhase.Landed, "Touchdown must enter Landed.");
Assert(controller.CanFinish, "A landed flight must be finishable.");
Assert(
    controller.LandingRateFeetPerMinute == 300,
    "Touchdown normal velocity must be converted from feet per second to FPM.");
Assert(
    controller.LandingGForce == 1.4,
    "Touchdown must capture the current G-force sample.");
Assert(controller.SimRateIncreased, "Landing must not clear simulation-rate tracking.");

controller.Finish();
Assert(controller.Phase == FlightPhase.Finished, "Finish must enter Finished.");

controller.ResetForNextFlight();
Assert(controller.Phase == FlightPhase.Ready, "Reset must return the controller to Ready.");
Assert(controller.FlightId is null, "Reset must clear the previous flight identifier.");
Assert(controller.StartedAt is null, "Reset must clear the previous start time.");
Assert(
    controller.LandingRateFeetPerMinute is null && controller.LandingGForce is null,
    "Reset must clear the previous landing values.");
Assert(!controller.SimRateIncreased, "Reset must restore normal-speed bonus eligibility.");
Assert(!controller.CanFinish, "A reset flight must not remain finishable.");
Assert(
    controller.EvaluateReadiness(true, contract, onGround) == "Ready to begin loading.",
    "A reset controller must allow the next eligible flight to start.");

controller.BeginLoading();
controller.Start(Guid.NewGuid(), onGround);
Assert(controller.Phase == FlightPhase.Started, "A second flight must start without restarting the app.");
var cancellation = controller.Observe(onGround with { FuelTotalKg = 112 });
Assert(cancellation?.Contains("Fuel was increased") == true, "Refuelling an active flight must cancel it.");
Assert(controller.Phase == FlightPhase.Cancelled, "A load violation must enter Cancelled.");
controller.ResetCancelledFlight();
Assert(controller.Phase == FlightPhase.Ready, "A cancelled session must be resettable.");
var restoredController = new FlightSessionController();
var restoredFlightId = Guid.NewGuid();
restoredController.Restore(
    new ActiveFlightSession(
        restoredFlightId,
        contract.ContractId,
        DateTimeOffset.UtcNow.AddMinutes(-20),
        HasAirborneTelemetry: true),
    onGround);
Assert(
    restoredController.FlightId == restoredFlightId
        && restoredController.Phase == FlightPhase.Landed
        && restoredController.CanFinish,
    "A restarted client must restore an airborne flight that is now on the ground.");
controller.BeginLoading();
controller.Start(Guid.NewGuid(), onGround);
var jumped = controller.Observe(onGround with { LatitudeDegrees = 53.3667 });
Assert(
    jumped?.Contains("position changed discontinuously") == true,
    "Reloading at another location must cancel the active flight.");

var groundFlickerController = new FlightSessionController();
groundFlickerController.BeginLoading();
groundFlickerController.Start(Guid.NewGuid(), onGround);
var groundFlicker = onGround with { OnGround = false, AltitudeAglFeet = 8 };
groundFlickerController.Observe(groundFlicker);
Assert(
    groundFlickerController.Phase == FlightPhase.Started,
    "A momentary off-ground reading below liftoff altitude must not enter Airborne.");
groundFlickerController.Observe(onGround);
Assert(
    groundFlickerController.Phase == FlightPhase.Started
        && !groundFlickerController.CanFinish,
    "Returning to the ground without a real takeoff must not enter Landed.");
var restoredFlickerController = new FlightSessionController();
restoredFlickerController.Restore(
    new ActiveFlightSession(
        Guid.NewGuid(),
        contract.ContractId,
        DateTimeOffset.UtcNow.AddMinutes(-5),
        HasAirborneTelemetry: true),
    groundFlicker);
Assert(
    restoredFlickerController.Phase == FlightPhase.Started,
    "A restored off-ground reading below liftoff altitude must not be treated as airborne.");

var resumedController = new FlightSessionController();
var beforeTelemetryGap = onGround with
{
    OnGround = false,
    GroundSpeedKnots = 250,
    IndicatedAirspeedKnots = 245,
};
resumedController.BeginLoading();
resumedController.Start(Guid.NewGuid(), beforeTelemetryGap);
var afterTelemetryGap = beforeTelemetryGap with
{
    ObservedAt = beforeTelemetryGap.ObservedAt.AddMinutes(2),
    LatitudeDegrees = beforeTelemetryGap.LatitudeDegrees + 0.2,
};
Assert(
    resumedController.Observe(afterTelemetryGap) is null,
    "Plausible movement after a telemetry interruption must not cancel the flight.");

resumedController.MarkServerSessionLost();
Assert(
    resumedController.Phase == FlightPhase.Cancelled && !resumedController.CanFinish,
    "A missing server session must immediately disable local flight completion.");
resumedController.ResetCancelledFlight();
Assert(
    resumedController.Phase == FlightPhase.Ready && resumedController.FlightId is null,
    "A lost server session must be resettable without restarting the desktop app.");

var payloadTelemetry = Sample(onGround: true, altitudeAgl: 5);
var payloadController = new FlightSessionController();
payloadController.BeginLoading();
payloadController.Start(Guid.NewGuid(), payloadTelemetry);
var asynchronousWeightSample = payloadTelemetry with
{
    ObservedAt = payloadTelemetry.ObservedAt.AddSeconds(1),
    FuelTotalKg = payloadTelemetry.FuelTotalKg - 20,
    TotalWeightPounds = payloadTelemetry.TotalWeightPounds - 10,
};
Assert(
    payloadController.Observe(asynchronousWeightSample) is null,
    "Normal fuel burn must not look like a payload change when weight values update asynchronously.");
var changedPayloadSample = asynchronousWeightSample with
{
    ObservedAt = asynchronousWeightSample.ObservedAt.AddSeconds(1),
    PayloadStationWeightPounds = asynchronousWeightSample.PayloadStationWeightPounds + 20,
};
Assert(
    payloadController.Observe(changedPayloadSample)?.Contains("payload changed") == true,
    "A real payload-station weight change must still cancel an active flight.");

// ── Mission phase driver guard rails ────────────────────────────────────
var missionDriveTest = MissionTestData.NewActiveMission("loading");
Assert(
    MissionPhaseDriver.FindPhase(missionDriveTest, "delivery") is { } deliveryPhase
        && MissionPhaseDriver.IsAction(deliveryPhase, MissionPhaseDriver.ActionCompleteMission),
    "The delivery phase should carry the complete_mission action.");
Assert(
    MissionPhaseDriver.NextPhase(missionDriveTest)?.Id == "departure",
    "The next phase should follow the authoring order.");
Assert(
    MissionPhaseDriver.NextPhase(MissionTestData.NewActiveMission("delivery")) is null,
    "The final phase must have no successor.");
Assert(
    MissionPhaseDriver.DialogText(missionDriveTest) == "The medics are loading the patient.",
    "The current phase dialog should resolve through its identifier.");
Assert(
    MissionPhaseDriver.PhaseElapsedSeconds(
        missionDriveTest with
        {
            PhaseData = new Dictionary<string, JsonElement>
            {
                ["phase_started_at"] = JsonSerializer.SerializeToElement(
                    DateTimeOffset.UtcNow.AddSeconds(-40)),
            },
        },
        DateTimeOffset.UtcNow) is { } phaseElapsed
        && phaseElapsed >= 39.9
        && phaseElapsed <= 41,
    "The phase timer should be measured from the server phase start.");
Assert(
    MissionPhaseDriver.ObjectsForPhase(
        missionDriveTest,
        MissionPhaseDriver.FindPhase(missionDriveTest, "pickup_scene")!)
        .Select(spec => spec.Id)
        .SequenceEqual(new[] { "obj_stretcher", "obj_nurse", "obj_vehicle" }),
    "spawn_objects should map authoring object identifiers to their full specifications.");

// ── Ground object spawner ───────────────────────────────────────────────
var fakeSimulator = new FakeSimulatorConnection(supportsObjectSpawning: true)
{
    ResolvedTitle = "Installed stretcher preset",
};
var spawner = new GroundObjectSpawner(fakeSimulator);
var spawnTestObject = MissionTestData.NewActiveMission("pickup_scene").Script.GroundObjects[0];
Assert(
    await spawner.TrySpawnAsync(spawnTestObject),
    "An enabled simulator should spawn a documented ground object.");
Assert(
    fakeSimulator.ResolveTitleCalls.SequenceEqual(new[] { ("stretcher", "ASO_Stretcher") })
        && fakeSimulator.SpawnCalls.SequenceEqual(new[] { "Installed stretcher preset" })
        && fakeSimulator.FreezeCalls.Count == 1,
    "A ground object should resolve to an installed preset and freeze exactly once after spawning.");
Assert(
    await spawner.TrySpawnAsync(spawnTestObject),
    "Re-requesting an already spawned object should be a no-op success.");
Assert(
    fakeSimulator.SpawnCalls.Count == 1,
    "Re-requesting an object must not spawn it twice.");
await spawner.RemoveAsync(spawnTestObject.Id);
Assert(
    spawner.Spawned.Count == 0 && fakeSimulator.RemoveCalls.Count == 1,
    "Removing a spawned object should clear it from the simulator and the tracker.");
var disabledSimulator = new FakeSimulatorConnection(supportsObjectSpawning: false);
var disabledSpawner = new GroundObjectSpawner(disabledSimulator);
Assert(
    !await disabledSpawner.TrySpawnAsync(spawnTestObject) && disabledSimulator.SpawnCalls.Count == 0,
    "Without SimConnect support the spawner should fail cleanly without a hang.");

var failingSimulator = new FakeSimulatorConnection { SpawnFailure = new InvalidOperationException("not an MSFS 2024 aircraft preset") };
var failingSpawner = new GroundObjectSpawner(failingSimulator);
Assert(
    !await failingSpawner.TrySpawnAsync(spawnTestObject),
    "A rejected spawn request should fail fast instead of hanging the pickup scene.");
Assert(
    failingSpawner.LastSpawnError is { Length: > 0 }
        && failingSpawner.LastSpawnError.Contains("not an MSFS 2024 aircraft preset", StringComparison.Ordinal),
    "The spawner should surface the simulator rejection reason.");

// ── MedEvac mission flow ────────────────────────────────────────────────
var missionSimulator = new FakeSimulatorConnection();
var missionSpawner = new GroundObjectSpawner(missionSimulator);
var missionClient = new FakeMissionClient();
TelemetrySnapshot? latestLoaded = null;
var completedOutcomeSeen = false;
var settlementRequests = 0;
var executor = new MissionExecutor(missionClient, missionSimulator, missionSpawner, () => latestLoaded);
executor.FlightSettlementRequested += (_, _) => settlementRequests++;
executor.MissionChanged += (_, snapshot) =>
{
    if (snapshot.Outcome == "completed")
    {
        completedOutcomeSeen = true;
    }
};
missionClient.Active = MissionTestData.NewActiveMission("briefing");

await executor.PollAsync();
Assert(
    missionClient.EvaluateCalls == 0 && missionClient.AdvanceCalls == 0,
    "Passive polling must leave the mission at its pre-flight briefing.");

await executor.InitializeForFlightAsync();
Assert(
    missionClient.Active?.CurrentPhaseId == "briefing"
        && missionSimulator.PositionCalls.Count == 0
        && missionSimulator.SpawnCalls.Count == 0
        && executor.CanAdvanceToNextPhase,
    "Starting the flight must arm the EDDM departure trigger and its debug action without moving the aircraft.");

latestLoaded = MissionTelemetry(
    MissionTestData.PickupLatitude + 1,
    MissionTestData.PickupLongitude,
    onGround: true);
await executor.AdvanceToNextPhaseAsync();
Assert(
    missionClient.Active?.CurrentPhaseId == "departure"
        && missionSimulator.PositionCalls.Count == 1,
    $"The EDDM debug action must teleport directly to Innsbruck and enter the pickup sequence "
        + $"(phase {missionClient.Active?.CurrentPhaseId}, positions {missionSimulator.PositionCalls.Count}).");
Assert(
    missionSimulator.SpawnCalls.SequenceEqual(
            new[] { "ASO_Stretcher", "ASO_Nurse", "ASO_Veh_MB_GWagen_01" })
        && missionSimulator.SpawnCalls.Count == 3
        && missionSimulator.FreezeCalls.Count == 2
        && missionSimulator.RemoveCalls.Count == 0,
    "The pickup phase should spawn every object and keep them staged through loading.");
Assert(
    missionClient.AdvanceCalls == 1,
    "Only the timed loading phase should use the explicit advance endpoint in this trigger-driven flow.");

var resumedSimulator = new FakeSimulatorConnection();
var resumedClient = new FakeMissionClient
{
    Active = MissionTestData.NewActiveMission("departure"),
};
var resumedExecutor = new MissionExecutor(
    resumedClient,
    resumedSimulator,
    new GroundObjectSpawner(resumedSimulator),
    () => null);
await resumedExecutor.PollAsync();
Assert(
    resumedSimulator.PositionCalls.Count == 0
        && resumedSimulator.SpawnCalls.Count == 3
        && resumedClient.AdvanceCalls == 0,
    "Restarting during departure must restore the server-recorded scene without moving the aircraft.");
Assert(resumedExecutor.CanReloadScene, "An initialized pickup scene should be reloadable.");
var revisedLatitude = MissionTestData.PickupLatitude + 0.01;
resumedClient.Active = resumedClient.Active! with
{
    Script = resumedClient.Active.Script with
    {
        GroundObjects = resumedClient.Active.Script.GroundObjects
            .Select(item => item.Id == "obj_stretcher"
                ? item with { Position = item.Position with { Lat = revisedLatitude } }
                : item)
            .ToArray(),
    },
};
await resumedExecutor.ReloadSceneAsync();
Assert(
    resumedSimulator.SpawnCalls.Count == 6
        && resumedSimulator.RemoveCalls.Count == 3
        && resumedSimulator.PositionCalls.Count == 0
        && resumedSimulator.SpawnPositions[3].Latitude == revisedLatitude,
    "Reloading must recreate every live scene object from the latest script definitions without repositioning the aircraft.");
await resumedExecutor.RestartAsync();
Assert(
    resumedClient.RestartCalls == 1
        && resumedClient.Active?.CurrentPhaseId == "briefing"
        && resumedSimulator.RemoveCalls.Count == 6,
    "Restarting must reset the server mission and remove the reconstructed scene.");
latestLoaded = MissionTelemetry(
    MissionTestData.DeliveryLatitude,
    MissionTestData.DeliveryLongitude,
    onGround: true);
await executor.AdvanceToNextPhaseAsync();
await executor.PollAsync();
Assert(
    missionSpawner.Spawned.Count == 2,
    $"The destination ambulance and stretcher must remain staged for handover (actual {missionSpawner.Spawned.Count}).");
Assert(
    !completedOutcomeSeen
        && missionClient.CompleteCalls == 0
        && missionClient.Active?.CurrentPhaseId == "delivery"
        && settlementRequests == 1,
    "The completed handover timer must request flight settlement exactly once without completing the contract itself.");
await executor.PollAsync();
Assert(settlementRequests == 1, "Repeated mission polling must not request flight settlement twice.");

missionClient.Active = null;
await executor.PollAsync();
missionClient.Active = MissionTestData.NewActiveMission("briefing");
await executor.PollAsync();
await executor.InitializeForFlightAsync();

latestLoaded = MissionTelemetry(
    MissionTestData.PickupLatitude,
    MissionTestData.PickupLongitude,
    onGround: true);
await executor.AdvanceToNextPhaseAsync();
Assert(
    missionClient.Active?.CurrentPhaseId == "departure",
    "A second mission on the same script must drive through the same phases again.");
Assert(
    missionSimulator.SpawnCalls.Count == 8 && missionSimulator.RemoveCalls.Count == 5,
    "A repeated mission must respawn the stage objects after a clean reset.");
latestLoaded = MissionTelemetry(
    MissionTestData.DeliveryLatitude,
    MissionTestData.DeliveryLongitude,
    onGround: true);
await executor.AdvanceToNextPhaseAsync();
await executor.PollAsync();
Assert(
    missionClient.CompleteCalls == 0,
    "Mission progression must never settle contracts independently.");

// ── Airport proximity gating ─────────────────────────────────────────────
var proximityClient = new FakeMissionClient();
var proximitySimulator = new FakeSimulatorConnection();
var proximitySpawner = new GroundObjectSpawner(proximitySimulator);
TelemetrySnapshot? proximityTelemetry = null;
var proximityExecutor = new MissionExecutor(
    proximityClient, proximitySimulator, proximitySpawner, () => proximityTelemetry);
proximityClient.Active = MissionTestData.NewActiveMission("briefing");
await proximityExecutor.PollAsync();
await proximityExecutor.InitializeForFlightAsync();
Assert(
    proximityClient.Active?.CurrentPhaseId == "briefing",
    "The mission must start at the briefing phase.");

proximityTelemetry = MissionTelemetry(
    MissionTestData.PickupLatitude,
    MissionTestData.PickupLongitude,
    onGround: true);
await proximityExecutor.AdvanceToNextPhaseAsync();
Assert(
    proximityClient.Active?.CurrentPhaseId == "departure",
    "Debug teleport must advance through to the departure phase.");

proximityTelemetry = MissionTelemetry(
    MissionTestData.DeliveryLatitude,
    MissionTestData.DeliveryLongitude,
    onGround: false);
await proximityExecutor.PollAsync();
Assert(
    proximityClient.Active?.CurrentPhaseId == "departure",
    "A departure from outside the pickup airport range must not advance the mission phase.");

proximityTelemetry = MissionTelemetry(
    MissionTestData.PickupLatitude + 0.01,
    MissionTestData.PickupLongitude,
    onGround: false);
await proximityExecutor.PollAsync();
Assert(
    proximityClient.Active?.CurrentPhaseId == "return_to_eddm",
    "A departure from within the pickup airport range must advance the mission phase.");

Console.WriteLine("VPN desktop mission lifecycle checks passed.");
return;

static TelemetrySnapshot Sample(
    bool onGround,
    double altitudeAgl,
    string aircraftTitle = "Cessna 172 Skyhawk",
    string aircraftAtcModel = "C172",
    string aircraftAtcType = "Cessna") => new(
    ObservedAt: DateTimeOffset.UtcNow,
    AircraftTitle: aircraftTitle,
    AircraftAtcModel: aircraftAtcModel,
    AircraftAtcType: aircraftAtcType,
    LatitudeDegrees: 52.3667,
    LongitudeDegrees: 13.5033,
    AltitudeFeet: 2500,
    AltitudeAglFeet: altitudeAgl,
    IndicatedAirspeedKnots: onGround ? 0 : 105,
    GroundSpeedKnots: onGround ? 0 : 110,
    VerticalSpeedFeetPerMinute: 0,
    HeadingTrueDegrees: 90,
    PitchDegrees: 0,
    BankDegrees: 0,
    OnGround: onGround,
    SlewActive: false,
    SimulationRate: 1,
    FuelTotalKg: 108.9,
    TotalWeightPounds: 2300,
    EmptyWeightPounds: 1663,
    EngineCount: 1,
    GearPositionPercent: 100,
    ParkingBrakeSet: onGround,
    PayloadStationWeightPounds: (2300d - 1663d) - 108.9d / 0.45359237d);

static TelemetrySnapshot MissionTelemetry(
    double latitudeDegrees,
    double longitudeDegrees,
    bool onGround) => new(
    ObservedAt: DateTimeOffset.UtcNow,
    AircraftTitle: "Cessna 208B Grand Caravan EX",
    AircraftAtcModel: "C208",
    AircraftAtcType: "Cessna",
    LatitudeDegrees: latitudeDegrees,
    LongitudeDegrees: longitudeDegrees,
    AltitudeFeet: 2500,
    AltitudeAglFeet: onGround ? 5 : 800,
    IndicatedAirspeedKnots: onGround ? 0 : 120,
    GroundSpeedKnots: onGround ? 0 : 125,
    VerticalSpeedFeetPerMinute: 0,
    HeadingTrueDegrees: 90,
    PitchDegrees: 0,
    BankDegrees: 0,
    OnGround: onGround,
    SlewActive: false,
    SimulationRate: 1,
    FuelTotalKg: 500,
    TotalWeightPounds: 8200,
    EmptyWeightPounds: 4500,
    EngineCount: 1,
    GearPositionPercent: 100,
    ParkingBrakeSet: onGround);

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}
