# Original RC-Combat Racer — Mini GDD

**Version:** v0.2 Prototype
**Author:** Joaquin Heredia  
**Engine:** Unity  
**Document Format:** Markdown (GitHub / VSCode Friendly)

---

# Table of Contents

1. Core Gameplay
2. Vehicle & Driving Design
3. Camera & Player View System
4. Weapons & Pickup System
5. Track & Environment Design
6. Race System & Game Modes
7. AI Driver System
8. UI & HUD System
9. Audio System
10. Visual Effects (VFX) System
11. Game Progression & Unlock System
12. Save System & Player Data
13. Input & Control System
14. Multiplayer System (Local & Online)
15. Physics System & Collision Model
16. Performance & Optimization Targets
17. Tools & Debug Systems
18. Technical Architecture & Project Structure
19. MVP Roadmap & Development Phases

---

# 1 — Core Gameplay

## Overview
Arcade-style RC racing game featuring weapon pickups, oversized environments, and fast-paced races.

## Core Pillars
- Tight Arcade Driving
- Environmental Scale Illusion
- Weaponized Racing
- Skill-Based Control

## Core Gameplay Loop
Select Race → Drive → Collect Pickup → Use Weapon → Complete Laps → Finish Race → Repeat

## Prototype Scope
- 4 fixed, learnable tracks with a pre-race track-selection menu
- 4 selectable vehicle families
- 2 Weapons
- 3 AI Opponents
- 1–4 player local split-screen, with optional keyboard ownership for solo testing
- Internet friend sessions for up to 4 racers

---

# 2 — Vehicle & Driving Design

## Vehicle Characteristics
- Lightweight handling
- High responsiveness
- Drift-capable turning
- Momentum-based physics

## Vehicle Components
- Engine System
- Steering System
- Suspension System
- Drift Controller
- Wheel Controllers

## Terrain Effects
Different surfaces affect friction and handling:
- Asphalt
- Dirt
- Grass
- Carpet
- Ice

---

# 3 — Camera & Player View System

## Default Camera
Third-person chase camera positioned slightly above vehicle.

## Camera Features
- Look-ahead prediction
- Smooth follow movement
- Obstacle avoidance
- Adjustable distance

---

# 4 — Weapons & Pickup System

## Weapon Types (MVP)
- Rocket Projectile
- Oil Slick Trap

## Pickup Behavior
- Spawn at fixed points
- Respawn after cooldown
- Single weapon slot per vehicle

## Weapon Effects
- Collision damage
- Temporary loss of control

---

# 5 — Track & Environment Design

## Track Features
- Oversized environments
- Shortcut routes
- Obstacles and hazards
- Multiple surface types

## Environment Types
- Indoor rooms
- Outdoor areas
- Toy-scale objects

---

# 6 — Race System & Game Modes

## Race Flow
Countdown → Racing → Final Lap → Finish → Results

## MVP Game Mode
- Standard Race
- 3 Laps

Future Modes:
- Time Trial
- Battle Mode
- Championship Mode

---

# 7 — AI Driver System

## AI Responsibilities
- Follow racing path
- Avoid obstacles
- Use weapons strategically
- Recover from errors

## AI Systems
- Waypoint Navigation
- Steering Logic
- Recovery Logic

---

# 8 — UI & HUD System

## HUD Elements
- Position Indicator
- Lap Counter
- Race Timer
- Weapon Indicator

## Menu Systems
- Main Menu
- Pause Menu
- Results Screen

---

# 9 — Audio System

## Core Audio Types
- Engine sounds
- Collision sounds
- Pickup sounds
- Weapon sounds
- Background music

## Audio Behavior
- Pitch shifts with speed
- Positional 3D sound

---

# 10 — Visual Effects (VFX) System

## Effects Include
- Dust trails
- Sparks
- Explosions
- Smoke effects
- Skid marks

## Trigger Sources
- Movement
- Collisions
- Weapon impacts

---

# 11 — Game Progression & Unlock System

## Unlock Types
- Vehicles
- Tracks
- Game Modes

## Unlock Methods
- Winning races
- Completing challenges

(MVP excludes progression complexity.)

---

# 12 — Save System & Player Data

## Saved Data
- Player progress
- Settings
- Best lap times

## Storage Format
- JSON
- Versioned files

---

# 13 — Input & Control System

## Supported Devices
- Keyboard
- Gamepad

## Input Actions
- Accelerate
- Brake
- Steer
- Fire Weapon
- Reset Vehicle

---

# 14 — Multiplayer System (Local & Online)

## Local Multiplayer
- Split-screen support

## Online Multiplayer (Prototype Requirement)
- Host/join friend-session flow for up to four racers
- Authoritative race, vehicle, combat, track-selection, results, and rematch synchronization
- Disconnect, timeout, and host-loss behavior with explicit recovery messaging
- Transport, authority topology, relay/discovery, and hybrid local-plus-online support require a dedicated architecture decision before implementation

Local split-screen remains a first-class mode and must not be broken or rebuilt
around the online transport. The first online acceptance target is a repeatable
private friend race over the internet; public matchmaking is not required.

---

# 15 — Physics System & Collision Model

## Core Physics Systems
- Rigidbody-based vehicle movement
- Wheel collider suspension
- Collision response

## Collision Handling
- Bounce effects
- Force transfer
- Stability correction

---

# 16 — Performance & Optimization Targets

## Target Performance
- 60 FPS minimum
- Stable physics updates

## Optimization Areas
- Physics simulation
- Rendering
- Memory usage

---

# 17 — Tools & Debug Systems

## Required Tools
- Debug HUD
- Vehicle tuning panel
- Physics visualization
- Waypoint visualization

## Debug Features
- Runtime parameter editing
- Performance monitoring

---

# 18 — Technical Architecture & Project Structure

## Folder Layout

```
Assets/
 ├── Art/
 ├── Audio/
 ├── Prefabs/
 ├── Scenes/
 ├── Scripts/
 ├── ScriptableObjects/
 └── Tools/
```

## Core Systems
- GameManager
- RaceManager
- VehicleManager
- AIManager
- WeaponManager
- UIManager
- SaveManager

## Architecture Goals
- Modularity
- Maintainability
- Data-driven configuration

---

# 19 — MVP Roadmap & Development Phases

## Phase List

0 — Project Setup  
1 — Core Driving Prototype  
2 — Basic Race Loop  
3 — AI Drivers  
4 — Weapons System  
5 — Track Polish  
6 — UI & HUD  
7 — Audio & VFX  
8 — Debug Tools  
9 — MVP Release

## MVP Deliverable

Playable race featuring:
- 1 Vehicle
- 1 Track
- 3 AI Drivers
- 2 Weapons
- Stable performance

---

# Appendix — MVP Summary Checklist

## Core Systems
- Vehicle movement functional
- Race loop operational
- AI drivers active
- Weapon system functional

## Visual Systems
- Camera working
- HUD functional
- Basic VFX implemented

## Stability
- No critical crashes
- Stable frame rate

---

# End of Document
