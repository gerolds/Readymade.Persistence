# Readymade.Persistence
A simple and very generic system for game state persistence. Uses a pull-architecture where the runtime state is created on demand when a save event is triggered. This allows distributed AND centralized state serialization. Built on top of a JSON serialized KeyValueStore and a very capable system for restoring static and dynamic objects.
