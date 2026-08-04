using Application.Auth;
using Application.Chat;
using Application.Comments;
using Application.Feed;
using Application.Follows;
using Application.Likes;
using Application.Posts;
using Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<PostService>();
        services.AddScoped<CommentService>();
        services.AddScoped<LikeService>();
        services.AddScoped<FollowService>();
        services.AddScoped<FeedService>();
        services.AddScoped<UserService>();
        services.AddScoped<ChatService>();
        return services;
    }
}
