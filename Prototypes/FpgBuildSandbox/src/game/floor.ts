import { SeededRng } from "./rng";
import type { FloorGraph, RewardKind, RoomNode } from "./types";

const makeNode = (
  id: string,
  column: number,
  row: number,
  type: RoomNode["type"],
  rewardKind: RewardKind,
  label: string,
  next: string[],
): RoomNode => ({ id, column, row, type, rewardKind, label, next, status: "locked" });

export function generateFloor(rng: SeededRng): FloorGraph {
  const lateRewards = rng.shuffle<RewardKind>(["item", "enchantment"]);
  const middleFunctions = rng.next() > 0.5
    ? [["merge", "合灵台"], ["recast", "重铸台"]] as const
    : [["recast", "重铸台"], ["merge", "合灵台"]] as const;

  const nodes: RoomNode[] = [
    makeNode("start", 0, 1, "start", "blessing", "诸神垂鉴", ["n1a", "n1b"]),
    makeNode("n1a", 1, 0, "combat", "item", "林缘伏击", ["n2a", "n2b"]),
    makeNode("n1b", 1, 2, "combat", "item", "雾径截杀", ["n2a", "n2b"]),
    makeNode("n2a", 2, 0, "combat", lateRewards[0] as RewardKind, "断木战场", ["shop", "xp", "station", "station-alt"]),
    makeNode("n2b", 2, 2, "combat", lateRewards[1] as RewardKind, "石坛围猎", ["shop", "xp", "station", "station-alt"]),
    makeNode("shop", 3, 0, "shop", "none", "行脚商店", ["elite"]),
    makeNode("xp", 3, 1, "experience", "none", "灵气藏", ["elite"]),
    makeNode("station", 3, 2, middleFunctions[0][0], "none", middleFunctions[0][1], ["elite"]),
    makeNode("station-alt", 3, 3, middleFunctions[1][0], "none", middleFunctions[1][1], ["elite"]),
    makeNode("elite", 4, 1, "elite", "blessing", "守坛者", ["boss"]),
    makeNode("boss", 5, 1, "boss", "none", "临时首领·蜃木魇", []),
  ];

  nodes.find((node) => node.id === "start")!.status = "current";
  return { nodes, startNodeId: "start", bossNodeId: "boss" };
}

export function completeNode(graph: FloorGraph, nodeId: string): void {
  const node = graph.nodes.find((candidate) => candidate.id === nodeId);
  if (!node) throw new Error(`Unknown floor node: ${nodeId}`);
  node.status = "complete";
  for (const nextId of node.next) {
    const next = graph.nodes.find((candidate) => candidate.id === nextId);
    if (next && next.status === "locked") next.status = "available";
  }
}

export function selectNode(graph: FloorGraph, nodeId: string): RoomNode {
  const node = graph.nodes.find((candidate) => candidate.id === nodeId);
  if (!node || node.status !== "available") throw new Error(`Node is not available: ${nodeId}`);
  for (const candidate of graph.nodes) if (candidate.status === "available") candidate.status = "locked";
  node.status = "current";
  return node;
}
