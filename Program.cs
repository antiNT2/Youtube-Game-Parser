using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace YoutubeParser
{
    internal class Program
    {
        static int numberOfVideos = 3;
        static Dictionary<string, ChannelInfo> channels = new Dictionary<string, ChannelInfo>();

        static void Main(string[] args)
        {
            List<string>? games = GetListOfGamesFromFile();

            List<ChannelWithVideos> channelsWithVideos = new List<ChannelWithVideos>();

            if (games == null || !games.Any())
            {
                return;
            }

            // Get the number of videos to search for
            Console.WriteLine($"Enter the number of videos to search for for each game (default is {numberOfVideos}):");
            string numberOfVideosInput = Console.ReadLine();
            if (!string.IsNullOrEmpty(numberOfVideosInput))
            {
                if (!int.TryParse(numberOfVideosInput, out numberOfVideos))
                {
                    Console.WriteLine("Invalid input. Using default value.");
                }
            }

            Console.WriteLine($"Searching for videos ({numberOfVideos} each) on the following games: {string.Join(", ", games)}");
            Console.WriteLine();

            //DebugPrint();

            //return;

            foreach (var game in games)
            {
                Console.WriteLine($"Searching for videos on {game}...");
                var videosOnCurrentGame = SearchVideosOnGame(game);

                if (videosOnCurrentGame.Any())
                {
                    // Iterate through the dictionary and add the videos to the list of channels
                    foreach (var channelVideosPair in videosOnCurrentGame)
                    {
                        var channel = channelVideosPair.Key;
                        var videos = channelVideosPair.Value;

                        var channelWithVideos = channelsWithVideos.FirstOrDefault(c => c.Channel.ChannelTitle == channel.ChannelTitle);

                        if (channelWithVideos == null)
                        {
                            channelWithVideos = new ChannelWithVideos { Channel = channel };
                            channelsWithVideos.Add(channelWithVideos);
                        }

                        channelWithVideos.Videos.Add(game, videos);
                    }

                    Console.WriteLine($"Found {videosOnCurrentGame.Count} channels with videos on {game}.");
                }

                Console.WriteLine();
            }

            Console.WriteLine("Channels with videos on the specified games:");

            // Order channels by number of games they have made videos on and then by number of videos and then by view count
            channelsWithVideos = channelsWithVideos.OrderByDescending(c => c.Videos.Count).ThenByDescending(c => c.Videos.Sum(v => v.Value.Count)).ToList();

            foreach (var channelWithVideos in channelsWithVideos)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{channelWithVideos.Channel}");
                Console.ResetColor();

                foreach (var gameVideosPair in channelWithVideos.Videos)
                {
                    var game = gameVideosPair.Key;
                    var videos = gameVideosPair.Value;

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"{game} ({videos.Count} videos)");
                    Console.ResetColor();

                    // Order videos by view count
                    videos = videos.OrderByDescending(v => v.ViewCount).ToList();

                    foreach (var video in videos)
                    {
                        Console.WriteLine($"\t {video}");
                    }

                    Console.WriteLine();
                }
            }

            WriteResultsToFile(channelsWithVideos);
            WriteResultsToSpreadsheet(channelsWithVideos);

            Console.ReadLine();
        }

        static void WriteResultsToFile(List<ChannelWithVideos> channelsWithVideos)
        {
            string filePath = "_results.txt";

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(filePath))
            {
                foreach (var channelWithVideos in channelsWithVideos)
                {
                    file.WriteLine($"{channelWithVideos.Channel}");

                    foreach (var gameVideosPair in channelWithVideos.Videos)
                    {
                        var game = gameVideosPair.Key;
                        var videos = gameVideosPair.Value;

                        file.WriteLine($"{game} ({videos.Count} videos)");

                        foreach (var video in videos)
                        {
                            file.WriteLine($"\t {video}");
                        }

                        file.WriteLine();
                    }
                }
            }

            Console.WriteLine($"Results written to {filePath}");
        }

        static void WriteResultsToSpreadsheet(List<ChannelWithVideos> channelsWithVideos)
        {
            string filePath = "_results.csv";

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(filePath))
            {
                file.WriteLine("Number of Subs, Name, Link, Covered Games");

                foreach (var channelWithVideos in channelsWithVideos)
                {
                    // We want the covered games to be in one cell with the name of the game and the number of videos in parentheses
                    string coveredGames = string.Join(" | ", channelWithVideos.Videos.Select(v => $"{v.Key} ({v.Value.Count})"));

                    file.WriteLine($"{channelWithVideos.Channel.SubscriberCount}, {channelWithVideos.Channel.ChannelTitle}, {channelWithVideos.Channel.ChannelLink}, {coveredGames}");
                }
            }

            Console.WriteLine($"Results written to {filePath}");
        }


        static void DebugPrint()
        {
            string game = "Valorant";
            List<Video> videos = new List<Video>();

            videos.Add(new Video { Channel = new ChannelInfo { ChannelTitle = "Channel 1", SubscriberCount = 100 }, VideoLink = "https://www.youtube.com/watch?v=1", VideoTitle = "Video 1", ViewCount = 1000 });
            videos.Add(new Video { Channel = new ChannelInfo { ChannelTitle = "Channel 1", SubscriberCount = 100 }, VideoLink = "https://www.youtube.com/watch?v=2", VideoTitle = "Video 2", ViewCount = 2000 });
            videos.Add(new Video { Channel = new ChannelInfo { ChannelTitle = "Channel 1", SubscriberCount = 100 }, VideoLink = "https://www.youtube.com/watch?v=3", VideoTitle = "Video 3", ViewCount = 3000 });
            videos.Add(new Video { Channel = new ChannelInfo { ChannelTitle = "Channel 1", SubscriberCount = 200 }, VideoLink = "https://www.youtube.com/watch?v=4", VideoTitle = "Video 4", ViewCount = 4000 });

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"{game} ({videos.Count} videos)");
            Console.ResetColor();

            // Order videos by view count
            videos = videos.OrderByDescending(v => v.ViewCount).ToList();

            foreach (var video in videos)
            {
                Console.WriteLine($"\t {video}");
            }
        }

        static Dictionary<ChannelInfo, List<Video>> SearchVideosOnGame(string gameName)
        {
            List<Video> foundVideos = SearchYouTubeChannels(gameName);

            // Regroup videos by channel
            Dictionary<ChannelInfo, List<Video>> channelVideos = new Dictionary<ChannelInfo, List<Video>>();
            foreach (var video in foundVideos)
            {
                if (!channelVideos.ContainsKey(video.Channel))
                {
                    channelVideos.Add(video.Channel, new List<Video>());
                }

                channelVideos[video.Channel].Add(video);
            }

            if (channelVideos.Any())
            {
                //Console.WriteLine("Channels that have made videos on " + gameName + " within the last 6 months:");
                //Console.WriteLine();

                // Order channels by subscriber count
                channelVideos = channelVideos.OrderByDescending(c => c.Key.SubscriberCount).ToDictionary(c => c.Key, c => c.Value);

                foreach (var channelVideosPair in channelVideos)
                {
                    var channel = channelVideosPair.Key;
                    var videos = channelVideosPair.Value;

                    //Console.ForegroundColor = ConsoleColor.Green;
                    //Console.WriteLine($"Channel Name: {channel.ChannelTitle} ({channel.SubscriberCount} subs) ({videos.Count} videos)");
                    //Console.ResetColor();

                    // Order videos by view count
                    videos = videos.OrderByDescending(v => v.ViewCount).ToList();

                    foreach (var video in videos)
                    {
                        //Console.WriteLine($"{video.ViewCount} views | {video.VideoTitle} | {video.VideoLink}");
                    }

                    //Console.WriteLine();
                }

                return channelVideos;
            }
            else
            {
                Console.WriteLine("No channels found for the specified game within the last 6 months.");
                return new Dictionary<ChannelInfo, List<Video>>();
            }
        }

        static List<Video> SearchYouTubeChannels(string gameName)
        {
            List<Video> videos = new List<Video>();

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = "AIzaSyDswqIAkQzCli2N3BtU34PWwhL6qxXeYBI",
                ApplicationName = "YouTubeGameSearch"
            });

            var searchListRequest = youtubeService.Search.List("snippet");
            searchListRequest.Q = $"{gameName} game";
            searchListRequest.MaxResults = numberOfVideos; // You can adjust this number based on your needs
            searchListRequest.Type = "video";
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Relevance;

            // Restrict to the last 6 months
            searchListRequest.PublishedAfterDateTimeOffset = DateTime.UtcNow.AddMonths(-6);

            var searchListResponse = searchListRequest.Execute();

            foreach (var searchResult in searchListResponse.Items)
            {
                var videoId = searchResult.Id.VideoId;
                var channelId = searchResult.Snippet.ChannelId;
                var videoLink = "https://www.youtube.com/watch?v=" + videoId;
                var videoTitle = searchResult.Snippet.Title;
                var viewCount = GetVideoViewCount(youtubeService, videoId);

                if (!channels.ContainsKey(channelId))
                {
                    var subscriberCount = GetChannelSubscriberCount(youtubeService, channelId);
                    var channelTitle = searchResult.Snippet.ChannelTitle;

                    var channelToAdd = new ChannelInfo { ChannelTitle = channelTitle, ChannelId = channelId, SubscriberCount = subscriberCount };
                    channels.Add(channelId, channelToAdd);
                }

                var channel = channels[channelId];

                videos.Add(new Video { Channel = channel, VideoLink = videoLink, VideoTitle = videoTitle, ViewCount = viewCount });
            }

            return videos;
        }

        static ulong GetVideoViewCount(YouTubeService youtubeService, string videoId)
        {
            var videoStatisticsRequest = youtubeService.Videos.List("statistics");
            videoStatisticsRequest.Id = videoId;
            var videoStatisticsResponse = videoStatisticsRequest.Execute();

            return videoStatisticsResponse.Items.FirstOrDefault()?.Statistics.ViewCount ?? 0;
        }

        static ulong GetChannelSubscriberCount(YouTubeService youtubeService, string channelId)
        {
            var channelStatisticsRequest = youtubeService.Channels.List("statistics");
            channelStatisticsRequest.Id = channelId; // Channel title serves as username here
            var channelStatisticsResponse = channelStatisticsRequest.Execute();

            Debug.WriteLine($"Channel id is {channelId}");

            return channelStatisticsResponse.Items?.FirstOrDefault()?.Statistics?.SubscriberCount ?? 0;
        }

        static List<string>? GetListOfGamesFromFile()
        {
            List<string> games = new List<string>();

            string gamesFilePath = "_games.txt";
            if (!System.IO.File.Exists(gamesFilePath))
            {
                Console.WriteLine($"Games file not found at {gamesFilePath}");
                return null;
            }

            string[] lines = System.IO.File.ReadAllLines(gamesFilePath);
            games.AddRange(lines);

            // Read the file and populate the list of games
            return games;
        }
    }

    class ChannelWithVideos
    {
        public ChannelInfo Channel { get; set; }

        /// <summary>
        /// The key is the game name and the value is the list of videos for that game
        /// </summary>
        public Dictionary<string, List<Video>> Videos { get; set; } = new Dictionary<string, List<Video>>();
    }

    class ChannelInfo
    {
        public string ChannelTitle { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;
        public ulong SubscriberCount { get; set; }

        public string ChannelLink => "https://www.youtube.com/channel/" + ChannelId;

        public override string ToString()
        {
            return $"{ChannelTitle} ({SubscriberCount} subs) | {ChannelLink}";
        }
    }

    class Video
    {
        public ChannelInfo Channel { get; set; }
        public string VideoTitle { get; set; }
        public ulong ViewCount { get; set; }
        public string VideoLink { get; set; }

        public override string ToString()
        {
            return $"{ViewCount} views | {VideoTitle} | {VideoLink}";
        }
    }

}

