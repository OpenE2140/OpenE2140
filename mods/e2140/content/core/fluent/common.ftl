## RushProtection
options-rush-protection-time =
    .no-limit = No protection
    .options =
        { $minutes ->
            [one] { $minutes } minute
           *[other] { $minutes } minutes
        }

label-rush-protection-time-countdown = Rush protection: {$time} remaining

notification-rush-protection-time-warning =
    { $minutes ->
        [one] Rush protection will be disabled in { $minutes } minute
        *[other] Rush protection will be disabled in { $minutes } minutes
    }

notification-rush-protection-disabled = Rush protection is disabled.
