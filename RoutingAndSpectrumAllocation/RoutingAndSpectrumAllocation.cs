﻿using RoutingAndSpectrumAllocation.Demands;
using RoutingAndSpectrumAllocation.Graphs;
using RoutingAndSpectrumAllocation.InputReaders;
using RoutingAndSpectrumAllocation.Loggers;
using RoutingAndSpectrumAllocation.RSA;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoutingAndSpectrumAllocation
{
    public class RoutingAndSpectrumAllocation : IRoutingAndSpectrumAllocation
    {
        IGraphInputReader InputReader { get; set; }
        IProgramLogger InfoLogger { get; set; }
        ILogger StorageLogger { get; set; }
        IPathSearcher PathSearcher { get; set; }
        IRSATableFill RSATableFill { get; set; }
        int supplied = 0;

        public RoutingAndSpectrumAllocation(
            IGraphInputReader inputReader, 
            IProgramLogger infologger, 
            ILogger storageLogger, 
            IPathSearcher pathSearcher,
            IRSATableFill RSATableFill)
        {
            InputReader = inputReader;
            InfoLogger = infologger;
            StorageLogger = storageLogger;
            PathSearcher = pathSearcher;
            this.RSATableFill = RSATableFill;
        }

        public async Task Start(string readNodesPath, string readLinksPath, int numberOfLinkChannels)
        {
            await InfoLogger.LogInformation($"Starting RoutingAndSpectrumAllocation\n");
            await InfoLogger.LogInformation($"Nodes Path: \"{readNodesPath}\"\n");
            await InfoLogger.LogInformation($"Links Path: \"{readLinksPath}\"\n");

            Graph graph = ReadGraph(readNodesPath, readLinksPath);

            await StorageLogger.WriteLog("graph", graph);

            List<Demand> demands = GetDemands(graph);

            await StorageLogger.WriteLog("demands", demands);

            await ApplyRSA(graph, demands, numberOfLinkChannels);
        }

        private async Task ApplyRSA(Graph graph, List<Demand> demands, int numberOfLinkChannels)
        {
            RSATable table = new RSATable(graph.Links, numberOfLinkChannels);

            foreach (Demand demand in demands)
            {
                GraphNode nodeFrom = graph.Nodes.FirstOrDefault(r => r.Id == demand.NodeIdFrom);
                GraphNode nodeTo = graph.Nodes.FirstOrDefault(r => r.Id == demand.NodeIdTo);

                await InfoLogger.LogInformation($"processing demand from {demand.NodeIdFrom} to {demand.NodeIdTo} with {demand.Slots} slots\n");

                List<GraphPath> paths = PathSearcher.GetPaths(graph, nodeFrom, nodeTo, 2);

                if (paths.Count() == 0)
                {
                    await InfoLogger.LogInformation($"path from {demand.NodeIdFrom} to {demand.NodeIdTo} not found.\n");
                    continue;
                }

                table = await FillTable(table, graph,  demand, paths);
            }

            await InfoLogger.LogInformation($"Finished\n");

            await InfoLogger.LogInformation($"Total Demands: {demands.Count}\nSupplied: {supplied}\nBlocked: {demands.Count - supplied}\n");

            await InfoLogger.LogInformation(table.ToStringTable());
        }

        private async Task<RSATable> FillTable(RSATable table, Graph graph, Demand demand, List<GraphPath> paths)
        {
            bool filled = false;
            foreach (var path in paths)
            {
                await InfoLogger.LogInformation($"trying path: {string.Join("->", path.Path)}");

                List<AvailableSlot> availableTableSlots = GetAvailableTableSlots(graph, path, table);

                if (RSATableFill.FillDemandOnTable(ref table, graph, demand, path, availableTableSlots))
                {
                    filled = true;
                    await InfoLogger.LogInformation($"demand supplied\n");
                    await InfoLogger.LogInformation(table.ToStringTable());
                    supplied++;
                    break;
                }
            }

            if (filled == false) 
                await InfoLogger.LogInformation($"It's not possible to supply demand of {demand.Slots}  from {demand.NodeIdFrom} to {demand.NodeIdTo}\n");
            return table;
        }

        private List<AvailableSlot> GetAvailableTableSlots(Graph graph, GraphPath path, RSATable table)
        {
            List<AvailableSlot> availableSlots = new List<AvailableSlot>();
            List<GraphLink> pathLinks = path.ToLinks(graph.Links);

            foreach (GraphLink link in pathLinks)
            {
                AvailableSlot element = new AvailableSlot();
                element.Link = link;
                element.Availables = new List<int>(table.Table[link.GetLinkId()].Where(r => r.Value.Values.Count == 0).Select(r => r.Key).ToList());
                availableSlots.Add(element);
            }

            return availableSlots;
        }

        private static List<Demand> GetDemands(Graph graph)
        {
            RandomDemandGenerator generator = new RandomDemandGenerator(graph.Links);
            return generator.GetDemands();
        }

        private Graph ReadGraph(string readNodesPath, string readLinksPath)
        {
            List<GraphNode> nodes = InputReader.GetNodes(readNodesPath);
            List<GraphLink> links = InputReader.GetLinks(readLinksPath);
            return new Graph(nodes, links);
        }
    }
}
