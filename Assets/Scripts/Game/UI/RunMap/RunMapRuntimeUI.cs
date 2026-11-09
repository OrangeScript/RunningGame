using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Running.Game.RunMap
{
    /// <summary>
    /// A vertically scrollable route. Visibility follows the authoritative vote
    /// and game phase, including synchronous host resolution and client updates.
    /// No delayed resolution callback can reopen it above gameplay or rewards.
    /// </summary>
    public sealed class RunMapRuntimeUI : MonoBehaviour
    {
        private const float LayerSpacing = 210f;
        private readonly Dictionary<int, Button> buttons = new Dictionary<int, Button>();
        private readonly Dictionary<int, TMP_Text> labels = new Dictionary<int, TMP_Text>();
        private readonly Dictionary<int, RouteNodeBadge> badges = new Dictionary<int, RouteNodeBadge>();
        private readonly Dictionary<int, Vector2> positions = new Dictionary<int, Vector2>();
        private RunMapNetworkCoordinator coordinator;
        private GameObject canvasObject;
        private RectTransform panel;
        private RectTransform content;
        private ScrollRect scroll;
        private TMP_Text timer;
        private TMP_Text votes;
        private TMP_Text status;
        private int displayedRound = -1;
        private bool subscribed;

        public bool IsVisible => panel != null && panel.gameObject.activeSelf;
        public ScrollRect RouteScroll => scroll;

        public void SetCoordinator(RunMapNetworkCoordinator value)
        {
            if (coordinator == value && subscribed) return;
            Unsubscribe();
            coordinator = value;
            Subscribe();
        }

        private void OnEnable()
        {
            if (coordinator == null) coordinator = GetComponent<RunMapNetworkCoordinator>();
            Subscribe();
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() { if (canvasObject != null) Destroy(canvasObject); }

        private void Subscribe()
        {
            if (coordinator == null || subscribed) return;
            coordinator.MapChanged += Rebuild;
            coordinator.VoteChanged += Refresh;
            coordinator.CurrentNodeChanged += HandleNodeSelected;
            coordinator.VoteResolved += HandleResolved;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (coordinator == null || !subscribed) return;
            coordinator.MapChanged -= Rebuild;
            coordinator.VoteChanged -= Refresh;
            coordinator.CurrentNodeChanged -= HandleNodeSelected;
            coordinator.VoteResolved -= HandleResolved;
            subscribed = false;
        }

        private bool ShouldShow()
        {
            if (coordinator?.VoteSnapshot == null || !coordinator.VoteSnapshot.IsActive) return false;
            var manager = GameManager.instance;
            return manager == null || manager.state == RunState.MapSelection;
        }

        private void Update()
        {
            bool visible = ShouldShow();
            if (visible && panel == null) EnsureView();
            if (panel != null && IsVisible != visible) panel.gameObject.SetActive(visible);
            if (!visible) return;
            var vote = coordinator.VoteSnapshot;
            timer.text = $"{Mathf.CeilToInt((float)vote.GetRemainingSeconds(Time.realtimeSinceStartupAsDouble)):00} 秒";
            if (displayedRound != vote.RoundId) Refresh(vote);
        }

        public void ShowMap()
        {
            EnsureView();
            panel.gameObject.SetActive(ShouldShow());
        }

        public void HideMap() { if (panel != null) panel.gameObject.SetActive(false); }
        private void HandleNodeSelected(RunMapNodeSnapshot node) => HideMap();
        private void HandleResolved(RunMapVoteResult result)
        {
            // A reward can open a subsequent vote synchronously. Consult its
            // round instead of hiding a newer vote because an older one ended.
            if (coordinator.VoteSnapshot?.RoundId == result.RoundId) HideMap();
        }

        private void Rebuild(RunMapSnapshot map)
        {
            EnsureView();
            buttons.Clear(); labels.Clear(); badges.Clear(); positions.Clear();
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                content.GetChild(i).gameObject.SetActive(false);
                Destroy(content.GetChild(i).gameObject);
            }
            if (map == null) return;
            int maxLayer = 0;
            var counts = new Dictionary<int, int>();
            foreach (var node in map.Nodes)
            {
                maxLayer = Mathf.Max(maxLayer, node.Layer);
                counts.TryGetValue(node.Layer, out int count);
                counts[node.Layer] = count + 1;
            }
            content.sizeDelta = new Vector2(0, maxLayer * LayerSpacing + 220);
            foreach (var node in map.Nodes)
                positions[node.Id] = new Vector2(
                    (node.Column - (counts[node.Layer] - 1) * 0.5f) * 205f,
                    110 + node.Layer * LayerSpacing);
            foreach (var edge in map.Connections)
                Connection(positions[edge.FromNodeId], positions[edge.ToNodeId]);
            foreach (var node in map.Nodes) CreateNode(node);
            for (int layer = 0; layer <= maxLayer; layer++)
            {
                var floor = MinimalUI.Text(content, "Floor", $"{layer + 1:00}", 15, MinimalUI.Muted);
                PositionInContent(floor.rectTransform, new Vector2(-460, 110 + layer * LayerSpacing), new Vector2(35, 30));
            }
            displayedRound = -1;
            Refresh(coordinator?.VoteSnapshot);
        }

        private void Connection(Vector2 from, Vector2 to)
        {
            Vector2 direction = (to - from).normalized;
            from += direction * 34; to -= direction * 34;
            var line = MinimalUI.Box(content, "Route", MinimalUI.Line);
            PositionInContent(line, (from + to) * 0.5f, new Vector2((to - from).magnitude, 1.5f));
            line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private void CreateNode(RunMapNodeSnapshot node)
        {
            int id = node.Id;
            var button = MinimalUI.Button(content, $"Node_{id}", string.Empty, () => Submit(id));
            PositionInContent((RectTransform)button.transform, positions[id], new Vector2(64, 64));
            button.GetComponent<Image>().color = Color.clear;
            var badgeObject = new GameObject("NodeRing",typeof(RectTransform),typeof(CanvasRenderer),typeof(RouteNodeBadge));
            badgeObject.transform.SetParent(button.transform,false);
            MinimalUI.Stretch((RectTransform)badgeObject.transform);
            var badge = badgeObject.GetComponent<RouteNodeBadge>();
            badge.raycastTarget = false;
            button.targetGraphic = badge;
            var iconObject = new GameObject("RouteIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RouteNodeIcon));
            iconObject.transform.SetParent(button.transform, false);
            var icon = iconObject.GetComponent<RouteNodeIcon>();
            icon.Type = node.Type;
            icon.color = MinimalUI.Ink;
            icon.raycastTarget = false;
            MinimalUI.Stretch((RectTransform)icon.transform, 16);
            var label = MinimalUI.Text(button.transform, "NodeType", NodeName(node.Type), 16,
                MinimalUI.Muted, TextAlignmentOptions.Center);
            MinimalUI.Fixed(label.rectTransform, new Vector2(0, -53), new Vector2(180, 32));
            buttons.Add(id, button); labels.Add(id, label); badges.Add(id,badge);
        }

        private static void PositionInContent(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void Submit(int id)
        {
            if (coordinator != null && coordinator.RequestVote(id) && ShouldShow())
                status.text = "已投票 · 等待队友，可重新选择";
            // Solo requests may already have resolved synchronously here.
            if (!ShouldShow()) HideMap();
        }

        private void Refresh(RunMapVoteSnapshot vote)
        {
            if (coordinator?.MapSnapshot == null) return;
            EnsureView();
            if (buttons.Count == 0) { Rebuild(coordinator.MapSnapshot); return; }
            bool newRound = vote != null && displayedRound != vote.RoundId;
            if (vote != null) displayedRound = vote.RoundId;
            var reachable = new HashSet<int>(coordinator.ReachableNodeIds);
            int ownVote = -1;
            var playerLines = new StringBuilder();
            if (vote != null)
            {
                foreach (var player in vote.PlayerVotes)
                {
                    bool own = NetworkManager.Singleton != null && player.PlayerId == NetworkManager.Singleton.LocalClientId;
                    if (own && player.HasVoted) ownVote = player.NodeId;
                    if (playerLines.Length > 0) playerLines.Append("     /     ");
                    playerLines.Append(own ? "你" : $"玩家 {player.PlayerId + 1}");
                    playerLines.Append(" · ");
                    playerLines.Append(player.HasVoted && coordinator.MapSnapshot.TryGetNode(player.NodeId, out var selected)
                        ? $"{NodeName(selected.Type)} · 路线 {selected.Column + 1}" : "选择中");
                }
            }
            votes.text = playerLines.ToString();
            foreach (var node in coordinator.MapSnapshot.Nodes)
            {
                var button = buttons[node.Id];
                bool available = reachable.Contains(node.Id);
                bool current = coordinator.CurrentNodeId == node.Id;
                button.interactable = vote != null && vote.IsActive && available;
                badges[node.Id].SetAppearance(available || current || node.Id == ownVote,
                    node.Id == ownVote || current);
                int tally = 0;
                if (vote != null) foreach (var candidate in vote.Candidates)
                    if (candidate.NodeId == node.Id) tally = candidate.VoteCount;
                labels[node.Id].text = NodeName(node.Type) + (tally > 0 ? $" · {tally} 票" : "");
                labels[node.Id].color = available || current ? MinimalUI.Ink : MinimalUI.Muted;
            }
            panel.gameObject.SetActive(ShouldShow());
            if (newRound)
            {
                status.text = "向上探索 · 滚轮或拖动查看路线";
                Canvas.ForceUpdateCanvases();
                float y = 0;
                foreach (int id in reachable) if (positions.TryGetValue(id, out Vector2 pos)) { y = pos.y; break; }
                float travel = Mathf.Max(1, content.rect.height - scroll.viewport.rect.height);
                scroll.verticalNormalizedPosition = Mathf.Clamp01((y - 170) / travel);
            }
        }

        public static string NodeName(RunMapNodeType type) => type switch
        {
            RunMapNodeType.Combat => "战斗", RunMapNodeType.Elite => "精英",
            RunMapNodeType.Event => "未知", RunMapNodeType.Reward => "奖励",
            RunMapNodeType.Treasure => "宝藏", _ => "首领"
        };

        private void EnsureView()
        {
            if (panel != null) return;
            canvasObject = MinimalUI.Canvas(transform, "RouteCanvas", 100);
            panel = MinimalUI.Box(canvasObject.transform, "RouteMap", MinimalUI.Paper, true);
            var eyebrow = MinimalUI.Text(panel, "Eyebrow", "R U N N I N G    /    行 程", 14, MinimalUI.Muted);
            MinimalUI.Place(eyebrow.rectTransform, new Vector2(.065f, .91f), new Vector2(.65f, .96f));
            var title = MinimalUI.Text(panel, "Title", "选择下一站", 36);
            MinimalUI.Place(title.rectTransform, new Vector2(.065f, .83f), new Vector2(.7f, .91f));
            timer = MinimalUI.Text(panel, "Timer", "", 21, MinimalUI.Muted, TextAlignmentOptions.MidlineRight);
            MinimalUI.Place(timer.rectTransform, new Vector2(.77f, .86f), new Vector2(.935f, .92f));
            MinimalUI.Rule(panel, .82f);
            var viewport = MinimalUI.Box(panel, "RouteViewport", MinimalUI.Paper, true);
            MinimalUI.Place(viewport, new Vector2(.17f, .16f), new Vector2(.83f, .80f));
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            content = new GameObject("RouteContent", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(1, 0);
            content.pivot = new Vector2(.5f, 0);
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 55; scroll.decelerationRate = .1f;
            MinimalUI.Rule(panel, .14f);
            votes = MinimalUI.Text(panel, "Votes", "", 17, MinimalUI.Ink);
            MinimalUI.Place(votes.rectTransform, new Vector2(.065f, .07f), new Vector2(.7f, .13f));
            status = MinimalUI.Text(panel, "Hint", "滚轮或拖动查看路线", 15, MinimalUI.Muted, TextAlignmentOptions.MidlineRight);
            MinimalUI.Place(status.rectTransform, new Vector2(.56f, .07f), new Vector2(.935f, .13f));
            var legend = MinimalUI.Text(panel, "Legend", "双剑  战斗      菱形  精英      ?  未知      礼盒  奖励      宝箱  宝藏      王冠  首领", 14, MinimalUI.Muted);
            MinimalUI.Place(legend.rectTransform, new Vector2(.065f, .02f), new Vector2(.935f, .07f));
            panel.gameObject.SetActive(false);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RouteNodeBadge : MaskableGraphic
    {
        private bool outlined;
        private bool selected;
        public void SetAppearance(bool showOutline,bool isSelected)
        {
            outlined=showOutline; selected=isSelected; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            const int segments=48;
            Vector2 center=rectTransform.rect.center;
            float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.49f;
            Color fill=selected ? new Color(.82f,.88f,.80f) : MinimalUI.Paper;
            Color stroke=outlined ? (selected ? MinimalUI.Accent : MinimalUI.Line) : MinimalUI.Paper;
            vh.AddVert(center,fill,Vector2.zero);
            for(int i=0;i<=segments;i++)
            {
                float angle=i*Mathf.PI*2/segments;
                Vector2 direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                vh.AddVert(center+direction*(radius-1),fill,Vector2.zero);
                vh.AddVert(center+direction*radius,stroke,Vector2.zero);
            }
            for(int i=0;i<segments;i++)
            {
                int inner=1+i*2; int next=inner+2;
                vh.AddTriangle(0,inner,next);
                vh.AddTriangle(inner,inner+1,next+1); vh.AddTriangle(inner,next+1,next);
            }
        }
    }

    /// <summary>Small vector line icons; no emoji/font-dependent game symbols.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RouteNodeIcon : MaskableGraphic
    {
        public RunMapNodeType Type;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            switch (Type)
            {
                case RunMapNodeType.Event:
                    Stroke(vh, -.25f,.3f, -.12f,.46f); Stroke(vh,-.12f,.46f,.2f,.4f);
                    Stroke(vh,.2f,.4f,.25f,.15f); Stroke(vh,.25f,.15f,0,-.05f);
                    Stroke(vh,0,-.05f,0,-.2f); Stroke(vh,0,-.4f,.02f,-.4f); break;
                case RunMapNodeType.Combat:
                    Stroke(vh,-.4f,-.4f,.35f,.4f); Stroke(vh,.4f,-.4f,-.35f,.4f);
                    Stroke(vh,-.42f,-.14f,-.14f,-.42f); Stroke(vh,.42f,-.14f,.14f,-.42f); break;
                case RunMapNodeType.Elite:
                    Stroke(vh,0,.5f,.4f,0); Stroke(vh,.4f,0,0,-.5f);
                    Stroke(vh,0,-.5f,-.4f,0); Stroke(vh,-.4f,0,0,.5f); Stroke(vh,0,.2f,0,-.2f); break;
                case RunMapNodeType.Boss:
                    Stroke(vh,-.4f,-.3f,.4f,-.3f); Stroke(vh,-.4f,-.3f,-.5f,.35f);
                    Stroke(vh,-.5f,.35f,-.15f,0); Stroke(vh,-.15f,0,0,.5f);
                    Stroke(vh,0,.5f,.15f,0); Stroke(vh,.15f,0,.5f,.35f); Stroke(vh,.5f,.35f,.4f,-.3f); break;
                default:
                    Stroke(vh,-.45f,-.35f,.45f,-.35f); Stroke(vh,-.45f,-.35f,-.45f,.25f);
                    Stroke(vh,.45f,-.35f,.45f,.25f); Stroke(vh,-.45f,.25f,.45f,.25f);
                    Stroke(vh,-.45f,0,.45f,0); Stroke(vh,0,.25f,0,-.2f);
                    if (Type == RunMapNodeType.Reward) { Stroke(vh,0,.25f,-.25f,.5f); Stroke(vh,0,.25f,.25f,.5f); }
                    break;
            }
        }
        private void Stroke(VertexHelper vh, float x1,float y1,float x2,float y2)
        {
            Rect rect = rectTransform.rect;
            float scale = Mathf.Min(rect.width, rect.height);
            Vector2 a = rect.center + new Vector2(x1,y1)*scale;
            Vector2 b = rect.center + new Vector2(x2,y2)*scale;
            Vector2 normal = new Vector2(-(b-a).y,(b-a).x).normalized * 1.2f;
            int start = vh.currentVertCount;
            vh.AddVert(a-normal,color,Vector2.zero); vh.AddVert(a+normal,color,Vector2.zero);
            vh.AddVert(b+normal,color,Vector2.zero); vh.AddVert(b-normal,color,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
        }
    }
}
