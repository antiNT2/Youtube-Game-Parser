using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace YoutubeParser
{
    internal class Program
    {

        static void Main(string[] args)
        {
            Console.WriteLine("Enter the name of the video game:");
            string gameName = Console.ReadLine();

            if (gameName == "" || gameName == null)
            {
                Console.WriteLine("Please enter a valid game name.");
                return;
            }

            List<Video> channels = SearchYouTubeChannels(gameName);

            // Regroup videos by channel
            Dictionary<Channel, List<Video>> channelVideos = new Dictionary<Channel, List<Video>>();
            foreach (var video in channels)
            {
                if (!channelVideos.ContainsKey(video.Channel))
                {
                    channelVideos.Add(video.Channel, new List<Video>());
                }

                channelVideos[video.Channel].Add(video);
            }

            if (channelVideos.Any())
            {
                Console.WriteLine("Channels that have made videos on " + gameName + " within the last 6 months:");
                Console.WriteLine();

                // Order channels by subscriber count
                channelVideos = channelVideos.OrderByDescending(c => c.Key.SubscriberCount).ToDictionary(c => c.Key, c => c.Value);

                foreach (var channelVideosPair in channelVideos)
                {
                    var channel = channelVideosPair.Key;
                    var videos = channelVideosPair.Value;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Channel Name: {channel.ChannelTitle} ({channel.SubscriberCount} subs) ({videos.Count} videos)");
                    Console.ResetColor();

                    // Order videos by view count
                    videos = videos.OrderByDescending(v => v.ViewCount).ToList();

                    foreach (var video in videos)
                    {
                        Console.WriteLine($"{video.ViewCount} views | {video.VideoTitle} | {video.VideoLink}");
                    }

                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("No channels found for the specified game within the last 6 months.");
            }

            Console.ReadLine();
        }

        static List<Video> SearchYouTubeChannels(string gameName)
        {
            List<Video> videos = new List<Video>();
            Dictionary<string, Channel> channels = new Dictionary<string, Channel>();

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = "AIzaSyDswqIAkQzCli2N3BtU34PWwhL6qxXeYBI",
                ApplicationName = "YouTubeGameSearch"
            });

            var searchListRequest = youtubeService.Search.List("snippet");
            searchListRequest.Q = $"{gameName} game";
            searchListRequest.MaxResults = 20; // You can adjust this number based on your needs
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

                    var channelToAdd = new Channel { ChannelTitle = channelTitle, SubscriberCount = subscriberCount };
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
    }

    class Channel
    {
        public string ChannelTitle { get; set; }
        public ulong SubscriberCount { get; set; }
    }

    class Video
    {
        public Channel Channel { get; set; }
        public string VideoTitle { get; set; }
        public ulong ViewCount { get; set; }
        public string VideoLink { get; set; }
    }

}

