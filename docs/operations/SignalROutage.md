# SignalR Outage

## Symptoms

- reconnect spikes
- realtime updates stop arriving
- join/leave operations fail

## Immediate Containment

- verify hub authentication and origin rules
- check whether the backend is healthy independently
- avoid duplicate subscriptions during reconnect

## Recovery

- restore the hub endpoint or upstream dependency
- confirm clients reconnect cleanly
