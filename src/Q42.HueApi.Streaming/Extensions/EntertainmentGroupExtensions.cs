using Q42.HueApi.ColorConverters;
using Q42.HueApi.Streaming.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Q42.HueApi.Streaming.Extensions
{
  public enum IteratorEffectMode
  {
    Cycle,
    Bounce,
    /// <summary>
    /// Only Once
    /// </summary>
    Single,
    Random,
    /// <summary>
    /// Apply the effect on all lights at the same time, ignoring different start states.
    /// Best for syncing all lights
    /// </summary>
    All,
    /// <summary>
    /// Apply the effect on all lights individually.
    /// Best used for example random colors to all lights
    /// </summary>
    AllIndividual
  }

  /// <summary>
  /// Function to apply light effects.
  /// </summary>
  /// <param name="current">Will contain 1 light, only contains multiple lights when IteratorEffectMode.All is used</param>
  /// <param name="timeSpan"></param>
  public delegate Task IteratorEffectFunc(IEnumerable<EntertainmentLight> current, TimeSpan? timeSpan = null);

  public static class EntertainmentGroupExtensions
  {
    public static IEnumerable<EntertainmentLight> GetLeft(this IEnumerable<EntertainmentLight> group)
    {
      return group.Where(x => x.LightLocation.IsLeft);
    }

    public static IEnumerable<EntertainmentLight> GetRight(this IEnumerable<EntertainmentLight> group)
    {
      return group.Where(x => x.LightLocation.IsRight);
    }

    public static IEnumerable<EntertainmentLight> GetFront(this IEnumerable<EntertainmentLight> group)
    {
      return group.Where(x => x.LightLocation.IsFront);
    }

    public static IEnumerable<EntertainmentLight> GetBack(this IEnumerable<EntertainmentLight> group)
    {
      return group.Where(x => x.LightLocation.IsBack);
    }

    /// <summary>
    /// X > -0.1 && X < 0.1
    /// </summary>
    /// <param name="group"></param>
    /// <returns></returns>
    public static IEnumerable<EntertainmentLight> GetCenter(this IEnumerable<EntertainmentLight> group)
    {
      return group.Where(x => x.LightLocation.IsCenter);
    }

    /// <summary>
    /// Apply the effectFunction repeatedly to a group of lights
    /// </summary>
    /// <param name="group"></param>
    /// <param name="effectFunction"></param>
    /// <param name="mode"></param>
    /// <param name="waitTime"></param>
    /// <param name="duration"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task IteratorEffect(this IEnumerable<EntertainmentLight> group, IteratorEffectFunc effectFunction, IteratorEffectMode mode, Ref<TimeSpan?> waitTime, TimeSpan? duration = null, CancellationToken cancellationToken = new CancellationToken())
    {
      if (waitTime == null)
        waitTime = TimeSpan.FromSeconds(1);
      if (duration == null)
        duration = TimeSpan.MaxValue;

      bool keepGoing = true;
      var lights = group.ToList();
      bool reverse = false;

      Stopwatch sw = new Stopwatch();
      sw.Start();

      while (keepGoing && !cancellationToken.IsCancellationRequested && !(sw.Elapsed > duration))
      {
        //Apply to whole group if mode is all
        if(mode == IteratorEffectMode.All)
        {
          await effectFunction(group, waitTime);

          await Task.Delay(waitTime.Value.Value, cancellationToken);

          continue;
        }

        if (reverse)
          lights.Reverse();
        if (mode == IteratorEffectMode.Random)
          lights = lights.OrderBy(x => Guid.NewGuid()).ToList();

        foreach(var light in lights.Skip(reverse ? 1 : 0))
        {
          if (!cancellationToken.IsCancellationRequested)
          {
            await effectFunction(new List<EntertainmentLight>() { light }, waitTime);

            if (mode != IteratorEffectMode.AllIndividual)
              await Task.Delay(waitTime.Value.Value, cancellationToken);
          }
        }

        if(mode == IteratorEffectMode.AllIndividual)
          await Task.Delay(waitTime.Value.Value, cancellationToken);

        keepGoing = mode == IteratorEffectMode.Single ? false : true;
        if (mode == IteratorEffectMode.Bounce)
          reverse = true;
      }
    }

    /// <summary>
    /// Apply the groupFunction repeatedly to a list of groups of lights
    /// </summary>
    /// <param name="list"></param>
    /// <param name="groupFunction"></param>
    /// <param name="mode"></param>
    /// <param name="waitTime"></param>
    /// <param name="duration"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task IteratorEffect(this IEnumerable<IEnumerable<EntertainmentLight>> list, IteratorEffectFunc groupFunction, IteratorEffectMode mode, Ref<TimeSpan?> waitTime, TimeSpan? duration = null, CancellationToken cancellationToken = new CancellationToken())
    {
      if (waitTime == null)
        waitTime = TimeSpan.FromSeconds(1);
      if (duration == null)
        duration = TimeSpan.MaxValue;

      bool keepGoing = true;
      var groups = list.ToList();
      bool reverse = false;

      Stopwatch sw = new Stopwatch();
      sw.Start();

      while (keepGoing && !cancellationToken.IsCancellationRequested && !(sw.Elapsed > duration))
      {
        //Apply to all groups if mode is all
        if (mode == IteratorEffectMode.All)
        {
          var flatGroup = list.SelectMany(x => x);
          if (!cancellationToken.IsCancellationRequested)
               await groupFunction(flatGroup, waitTime);

            //foreach (var group in list)
            //{
            //  if (!cancellationToken.IsCancellationRequested)
            //    await groupFunction(group, waitTime);
            //}

            await Task.Delay(waitTime.Value.Value);

          continue;
        }

        if (reverse)
          groups.Reverse();
        if (mode == IteratorEffectMode.Random)
          groups = groups.OrderBy(x => Guid.NewGuid()).ToList();

        foreach (var group in groups.Skip(reverse ? 1 : 0))
        {
          await groupFunction(group, waitTime);

          if (mode != IteratorEffectMode.AllIndividual)
            await Task.Delay(waitTime.Value.Value, cancellationToken);
        }

        if (mode == IteratorEffectMode.AllIndividual)
          await Task.Delay(waitTime.Value.Value, cancellationToken);

        keepGoing = mode == IteratorEffectMode.Single ? false : true;
        if (mode == IteratorEffectMode.Bounce)
          reverse = true;
      }
    }



    /// <summary>
    /// Brightness between 0 and 1
    /// </summary>
    /// <param name="group"></param>
    /// <param name="brightness">Between 0 and 1</param>
    /// <param name="transitionTime"></param>
    /// <param name="inSync">Syncs the transition over all lights. Set to false if each light has a different starting rgb/bri for the transition</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static IEnumerable<EntertainmentLight> SetBrightness(this IEnumerable<EntertainmentLight> group,
      double brightness, TimeSpan transitionTime = default(TimeSpan), bool inSync = true, CancellationToken cancellationToken = default(CancellationToken))
    {
      group.SetState(null, brightness, transitionTime, inSync, cancellationToken);

      return group;
    }

    /// <summary>
    /// Transition to new RGB Color
    /// </summary>
    /// <param name="group"></param>
    /// <param name="rgb"></param>
    /// <param name="transitionTime"></param>
    /// <param name="inSync">Syncs the transition over all lights. Set to false if each light has a different starting rgb/bri for the transition</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static IEnumerable<EntertainmentLight> SetColor(this IEnumerable<EntertainmentLight> group,
      RGBColor rgb, TimeSpan transitionTime = default(TimeSpan), bool inSync = true, CancellationToken cancellationToken = default(CancellationToken))
    {
      group.SetState(rgb, null, transitionTime, inSync, cancellationToken);

      return group;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="group"></param>
    /// <param name="rgb"></param>
    /// <param name="brightness"></param>
    /// <param name="transitionTime"></param>
    /// <param name="inSync">Syncs the transition over all lights. Set to false if each light has a different starting rgb/bri for the transition</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static IEnumerable<EntertainmentLight> SetState(this IEnumerable<EntertainmentLight> group,
      RGBColor? rgb = null, double? brightness = null, TimeSpan transitionTime = default(TimeSpan), bool inSync = true, CancellationToken cancellationToken = default(CancellationToken))
    {
      //Re-use the same transition for all lights so transition is in sync. The transition will use the start rgb/bri from the first light in the group.
      if (inSync)
      {
        //Create a new transition
        Transition transition = EntertainmentLightExtensions.CreateTransition(rgb, brightness, transitionTime);

        //Add the same transition to all lights in this group
        foreach (var light in group)
        {
          if(!cancellationToken.IsCancellationRequested)
            light.Transitions.Add(transition);
        }

        //Start the transition
        var firstLight = group.FirstOrDefault();
        transition.Start(firstLight.State.RGBColor, firstLight.State.Brightness, cancellationToken);
      }
      else
      {
        foreach (var light in group)
        {
          if (!cancellationToken.IsCancellationRequested)
            light.SetState(rgb, brightness, transitionTime, cancellationToken);
        }
      }

      return group;
    }
  }
}