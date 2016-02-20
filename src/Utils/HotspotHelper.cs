using System;
using JetBrains.Annotations;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.LiveTemplates;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.TextControl;
using JetBrains.Util;

namespace BananaSplit
{
  internal static class HotspotHelper
  {
    public static Action<ITextControl> ExecuteHotspotSession(
      [NotNull] ISolution solution, [NotNull] HotspotInfo[] hotspots, [CanBeNull] Action<ITextControl> onFinish = null)
    {
      return textControl =>
      {
        var templatesManager = Shell.Instance.GetComponent<LiveTemplatesManager>();
        var escapeAction = LiveTemplatesManager.EscapeAction.LeaveTextAndCaret;

        var hotspotSession = templatesManager.CreateHotspotSessionAtopExistingText(solution,
          TextRange.InvalidRange, textControl, escapeAction, hotspots);

        if (onFinish != null)
        {
          hotspotSession.Closed.Advise(EternalLifetime.Instance, closedEventArgs =>
          {
            if (closedEventArgs.TerminationType == TerminationType.Finished)
            {
              onFinish(textControl);
            }
          });
        }

        hotspotSession.Execute();
      };
    }
  }
}