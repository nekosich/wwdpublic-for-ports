device-pda-slot-component-slot-name-cartridge = Cartridge

default-program-name = Program
notekeeper-program-name = Notekeeper
nano-task-program-name = NanoTask
news-read-program-name = Station news

crew-manifest-program-name = Crew manifest
crew-manifest-cartridge-loading = Loading ...

net-probe-program-name = NetProbe
net-probe-scan = Scanned {$device}!
net-probe-label-name = Name
net-probe-label-address = Address
net-probe-label-frequency = Frequency
net-probe-label-network = Network

log-probe-program-name = LogProbe
log-probe-scan = Downloaded logs from {$device}!
log-probe-label-time = Time
log-probe-label-accessor = Accessed by
log-probe-label-number = #
log-probe-print-button = Print Logs
log-probe-printout-device = Scanned Device: {$name}
log-probe-printout-header = Latest logs:
log-probe-printout-entry = #{$number} / {$time} / {$accessor}

astro-nav-program-name = AstroNav

med-tek-program-name = MedTek

# NanoTask cartridge

nano-task-ui-heading-high-priority-tasks =
    { $amount ->
        [zero] No High Priority Tasks
        [one] 1 High Priority Task
       *[other] {$amount} High Priority Tasks
    }
nano-task-ui-heading-medium-priority-tasks =
    { $amount ->
        [zero] No Medium Priority Tasks
        [one] 1 Medium Priority Task
       *[other] {$amount} Medium Priority Tasks
    }
nano-task-ui-heading-low-priority-tasks =
    { $amount ->
        [zero] No Low Priority Tasks
        [one] 1 Low Priority Task
       *[other] {$amount} Low Priority Tasks
    }
nano-task-ui-done = Done
nano-task-ui-revert-done = Undo
nano-task-ui-priority-low = Low
nano-task-ui-priority-medium = Medium
nano-task-ui-priority-high = High
nano-task-ui-cancel = Cancel
nano-task-ui-print = Print
nano-task-ui-delete = Delete
nano-task-ui-save = Save
nano-task-ui-new-task = New Task
nano-task-ui-description-label = Description:
nano-task-ui-description-placeholder = Get something important
nano-task-ui-requester-label = Requester:
nano-task-ui-requester-placeholder = John Nanotrasen
nano-task-ui-item-title = Edit Task
nano-task-printed-description = Description: {$description}
nano-task-printed-requester = Requester: {$requester}
nano-task-printed-high-priority = Priority: High
nano-task-printed-medium-priority = Priority: Medium
nano-task-printed-low-priority = Priority: Low

# Wanted list cartridge
wanted-list-program-name = Wanted list
wanted-list-label-no-records = It's all right, cowboy
wanted-list-search-placeholder = Search by name and status

wanted-list-age-label = [color=darkgray]Age:[/color] [color=white]{$age}[/color]
wanted-list-job-label = [color=darkgray]Job:[/color] [color=white]{$job}[/color]
wanted-list-species-label = [color=darkgray]Species:[/color] [color=white]{$species}[/color]
wanted-list-gender-label = [color=darkgray]Gender:[/color] [color=white]{$gender}[/color]

wanted-list-reason-label = [color=darkgray]Reason:[/color] [color=white]{$reason}[/color]
wanted-list-unknown-reason-label = unknown reason

wanted-list-initiator-label = [color=darkgray]Initiator:[/color] [color=white]{$initiator}[/color]
wanted-list-unknown-initiator-label = unknown initiator

wanted-list-status-label = [color=darkgray]status:[/color] {$status ->
        [suspected] [color=yellow]suspected[/color]
        [wanted] [color=red]wanted[/color]
        [detained] [color=#b18644]detained[/color]
        [paroled] [color=green]paroled[/color]
        [discharged] [color=green]discharged[/color]
        *[other] none
    }

wanted-list-history-table-time-col = Time
wanted-list-history-table-reason-col = Crime
wanted-list-history-table-initiator-col = Initiator

glimmer-monitor-program-name = Glimmer Monitor
glimmer-monitor-current-glimmer = Current Glimmer: {$glimmer}Ψ
glimmer-monitor-interval = Interval
glimmer-monitor-sync = Sync

brain-wave-scanner-program-name = Brain Wave Scanner
brain-wave-scanner-title = Brain Wave Scanner
brain-wave-scanner-theta = Theta Drift
brain-wave-scanner-gamma = Gamma Spikes
brain-wave-scanner-coherence = Coherence Drop
brain-wave-scanner-noise = Noise Index
brain-wave-scanner-conductivity = Stress Conductivity
brain-wave-scanner-no-scans = No scans recorded.
brain-wave-scanner-entry = {$subject}: θ {$theta} | γ {$gamma} | coh {$coherence} | noise {$noise}
brain-wave-scanner-scan = Brain-wave profile recorded for {$target}.

psych-interpret-program-name = Psych Interpreter
psych-interpret-title = Cognitive Pattern Interpreter
psych-interpret-symptoms-title = Reported Symptoms
psych-interpret-run = Analyze Pattern
psych-interpret-pattern = Pattern Code
psych-interpret-confidence = Confidence
psych-interpret-protocol = Suggested Protocol
psych-interpret-last-scan-none = Last scan: none
psych-interpret-last-scan = Last scan: {$target}
psych-interpret-popup-no-scan = No brain-wave scan data found.
psych-interpret-notes-no-scan = Insufficient signal data. Collect a fresh scan and repeat.
psych-interpret-notes-remission = Low anomaly footprint. Recommend observation protocol.
psych-interpret-notes-prodromal = Emerging instability markers detected. Early intervention advised.
psych-interpret-notes-active = Sustained instability pattern detected. Begin active protocol.
psych-interpret-notes-break = Extreme instability profile detected. Immediate intensive protocol required.
psych-interpret-symptom-auditory_whispers = Auditory whispers
psych-interpret-symptom-visual_phantoms = Visual phantoms
psych-interpret-symptom-derealization = Derealization
psych-interpret-symptom-paranoid_dread = Paranoid dread
psych-interpret-symptom-thought_fragmentation = Thought fragmentation
