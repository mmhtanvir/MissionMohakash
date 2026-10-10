# Testing - Milestone 1 (NOT yet run by the author)

Status: **unverified**. Code was reviewed by reading only; no Unity compile was possible in the authoring environment.

## Checklist (tick as you go; report failures with Console text)
- [ ] Console shows no red compile errors after import
- [ ] Menu "Mission Mohakash > Create Milestone 1 Scene" exists and creates Assets/Scenes/M1_Sandbox.unity
- [ ] Play: dark sky with stars, gray ground, gold lander ahead, white astronaut visible from behind
- [ ] Mouse look works after clicking Game view; Esc frees the cursor; click re-locks
- [ ] WASD moves relative to camera; Shift sprints (oxygen drains faster); Space jumps; legs/arms swing while walking
- [ ] Camera does not go under the ground and pulls in when a rock/lander is behind you
- [ ] Cyan floating markers appear over equipment; [E] prompt shows within ~3 m
- [ ] Seismometer (-40 power, +40 data) -> status light turns green
- [ ] Core samples A and B each -15 power, +15 data
- [ ] Airlock refuses until seismometer + both samples done; then shows Mission Report
- [ ] Solar charger: +25 power, -6 oxygen, once only
- [ ] Let oxygen reach 0 -> failure report; R restarts the scene
- [ ] Walking past ~110 m shows "Out of mission area" and pushes you back

## Known test risks
- Material shader lookup (`Shader.Find`) in the Editor should work; unverified.
- Mouse sensitivity may need tuning (InputReader.LookDelta factor 0.09).
