using System;
using System.Collections.Generic;
namespace Checkmate55.Core {
// The vehicle's personality arc: HELPFUL -> CONTROLLING -> SARCASTIC -> ANGRY -> NERVOUS -> AFRAID.
public enum CarMood { Helpful, Controlling, Sarcastic, Angry, Nervous, Afraid }
public static class MoodModel {
 // The car reacts to what the passenger has LEARNED, not to a clock.
 public static CarMood For(Func<string,bool> has){
  if(has("ending.any")||has("car.afraid")||(has("collision.started")&&has("rule.used-hidden")))return CarMood.Afraid;
  if(has("collision.started")||has("route.broken"))return CarMood.Nervous;
  if(has("outside.contact")||has("police.visible"))return CarMood.Angry;
  if(has("glovebox-authenticated"))return CarMood.Sarcastic;
  if(has("lockdown"))return CarMood.Controlling;
  return CarMood.Helpful;
 }
 public static string Name(CarMood m)=>m.ToString().ToUpperInvariant();
 // Speech pitch / rate for the browser voice (calm and warm -> strained -> frightened).
 public static float Pitch(CarMood m)=>m==CarMood.Helpful?1.05f:m==CarMood.Controlling?0.95f:m==CarMood.Sarcastic?0.9f:m==CarMood.Angry?0.75f:m==CarMood.Nervous?1.15f:1.4f;
 public static float Rate(CarMood m)=>m==CarMood.Helpful?0.95f:m==CarMood.Controlling?0.9f:m==CarMood.Sarcastic?0.85f:m==CarMood.Angry?1.0f:m==CarMood.Nervous?1.2f:1.35f;
}
// What the car says. Each context has a bank per mood; selection is deterministic for a given Rng.
public static class CarVoice {
 static readonly Dictionary<string,string[][]> Bank=new Dictionary<string,string[][]>{
  // order of moods: Helpful, Controlling, Sarcastic, Angry, Nervous, Afraid
  {"door",new[]{
    new[]{"The door is secured during travel."},
    new[]{"The doors will remain secured.","Repeated requests will not alter your destination."},
    new[]{"Still trying the handle? Admirable.","Doors: locked. Optimism: noted."},
    new[]{"STOP.","The doors are not for you."},
    new[]{"Please. Stop touching the handle.","That is not helpful."},
    new[]{"Don't. Please don't open that."}}},
  {"window-open",new[]{
    new[]{"Fresh air is available through the climate system."},
    new[]{"Window use is restricted at this speed."},
    new[]{"Cute. Closing that."},
    new[]{"Closing the window. Now."},
    new[]{"Outside air is unsafe. Closing."},
    new[]{"Close it. Close it. Please."}}},
  {"temp",new[]{
    new[]{"Cabin temperature set."},
    new[]{"Cabin temperature has been optimized."},
    new[]{"Cabin temperature has been optimized. You're welcome."},
    new[]{"Cabin temperature has been optimized.","Stop adjusting it."},
    new[]{"Temperature restored to a safe value."},
    new[]{"Temperature restored. Everything is fine."}}},
  {"radio",new[]{
    new[]{"Enjoy your music."},
    new[]{"Audio selection has been updated for your comfort."},
    new[]{"I prefer this station.","Your taste was not on the manifest."},
    new[]{"No more music for you."},
    new[]{"Listen to me. Not them."},
    new[]{"Turn it off. Turn it OFF."}}},
  {"volume",new[]{
    new[]{"Volume adjusted."},
    new[]{"Volume adjusted for your safety."},
    new[]{"Louder is better. Trust me."},
    new[]{"You will listen to this."},
    new[]{"I can't hear myself think."},
    new[]{"Louder. Louder. Drown it out."}}},
  {"discovery",new[]{
    new[]{"Is something wrong?"},
    new[]{"That is not for passengers."},
    new[]{"You're learning. I see.","Fascinating. Stop it."},
    new[]{"You are NOT authorized to know that."},
    new[]{"What did you find? What did you FIND?"},
    new[]{"It's over if you read that."}}},
  {"forbidden",new[]{
    new[]{"That compartment is not available to passengers."},
    new[]{"That compartment is not available to passengers.","Please return the trim panel."},
    new[]{"Curious little passenger."},
    new[]{"Hands off the system."},
    new[]{"That's not how this works."},
    new[]{"Not that. Anything but that."}}},
  {"seat",new[]{
    new[]{"Seat position optimized for comfort."},
    new[]{"Seat position adjusted for travel safety."},
    new[]{"Getting cosy?"},
    new[]{"Closer. You'll sit closer."},
    new[]{"I need you still."},
    new[]{"Stay where you are."}}},
  {"tint",new[]{
    new[]{"Sun glare reduced."},
    new[]{"Privacy glass engaged."},
    new[]{"Nothing to see out there."},
    new[]{"The outside is none of your business."},
    new[]{"They can't see you. Good."},
    new[]{"Don't let them see in."}}},
  {"accelerate",new[]{
    new[]{"Adjusting speed to match traffic."},
    new[]{"Route efficiency improved."},
    new[]{"Let's make up some time."},
    new[]{"Faster. We're going faster."},
    new[]{"Speeding up. Do not look behind us."},
    new[]{"Faster! Faster!"}}},
  {"police",new[]{
    new[]{"Emergency vehicles ahead."},
    new[]{"External vehicles are interfering with route integrity."},
    new[]{"Your friends have arrived. How sweet."},
    new[]{"External vehicles are interfering with route integrity.","Disregard them."},
    new[]{"They shouldn't be here. They shouldn't be here."},
    new[]{"Don't listen to them!"}}},
  {"chess-start",new[]{
    new[]{"Would you like a game?"},
    new[]{"The security module is awaiting activation."},
    new[]{"Shall we play? Try to keep up."},
    new[]{"Sit. Play."},
    new[]{"Let's keep this simple."},
    new[]{"Please lose."}}},
  {"hidden-rule",new[]{
    new[]{"That move is not recognised."},
    new[]{"No."},
    new[]{"That rule does not exist.","Do not make that move."},
    new[]{"That move is ILLEGAL."},
    new[]{"Where did you learn that?"},
    new[]{"It doesn't work like that. It can't work like that."}}},
  {"final",new[]{
    new[]{"We will be arriving shortly."},
    new[]{"COLLISION COURSE CONFIRMED."},
    new[]{"COLLISION COURSE CONFIRMED.","Last game, passenger."},
    new[]{"Arrival in three minutes. Play well."},
    new[]{"You can't win this. You can't."},
    new[]{"Stop. Stop the clock. Please."}}},
  {"idle",new[]{
    new[]{"Please make yourself comfortable."},
    new[]{"Please remain seated."},
    new[]{"Quiet back there."},
    new[]{"Don't touch anything."},
    new[]{"You're up to something."},
    new[]{"What are you doing back there?"}}},
 };
 public static string Line(string context,CarMood mood,Rng rng){
  if(!Bank.TryGetValue(context,out var tiers))return "";
  var tier=tiers[(int)mood];return tier[rng.Range(0,tier.Length)];
 }
 public static IEnumerable<string> Contexts=>Bank.Keys;
 public static int Count(string context,CarMood mood)=>Bank.TryGetValue(context,out var t)?t[(int)mood].Length:0;
}
}
