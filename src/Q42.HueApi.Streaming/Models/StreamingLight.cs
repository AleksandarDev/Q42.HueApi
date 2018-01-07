using Q42.HueApi.ColorConverters;
using Q42.HueApi.Models.Groups;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Q42.HueApi.Streaming.Models
{
  public class StreamingLight
  {
    public LightLocation LightLocation { get; private set; }

    public byte Id { get; set; }

    public StreamingState State { get; set; } = new StreamingState();

    public List<Transition> Transitions { get; set; } = new List<Transition>();


    public StreamingLight(string id, LightLocation location = null)
    {
      Id = byte.Parse(id);
      LightLocation = location;
    }

    internal IEnumerable<byte> GetState()
    {
      ProcessTransitions();

      List<byte> result = new List<byte>();

      byte deviceType = 0x00; //Type of device 0x00 = Light; 0x01 = Area
      var lightIdBytes = BitConverter.GetBytes(this.Id);

      result.Add(deviceType);
      result.Add(0x00);
      result.Add(this.Id);
      result.AddRange(this.State.ToByteArray());

      return result;
    }

    /// <summary>
    /// Changes the state based on one or more transition
    /// </summary>
    private void ProcessTransitions()
    {
      var finishedStates = Transitions.Where(x => x.IsFinished);

      if (finishedStates.Any())
      {
        foreach (var finished in finishedStates)
        {
          this.State.SetBrightnes(finished.TransitionState.Brightness);
          this.State.SetRGBColor(finished.TransitionState.RGBColor);
        }

        //Cancel and remove all transitions, last finished state is important
        Transitions.Clear();
      }
      else
      {
        //Get active transition
        var activeTransition = Transitions.Where(x => x.TransitionState.IsDirty).LastOrDefault();
        if (activeTransition != null)
        {
          this.State.SetBrightnes(activeTransition.TransitionState.Brightness);
          this.State.SetRGBColor(activeTransition.TransitionState.RGBColor);
        }
      }
    }
  }
}
