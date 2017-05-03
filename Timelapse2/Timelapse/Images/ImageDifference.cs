namespace Timelapse.Images
{
    // These are used for image differencing
    // If a person toggles between the current image and its two differenced imaes, those images are stored
    // in a 'cache' so they can be redisplayed more quickly (vs. re-reading it from a file or regenerating it)
    public enum ImageDifference
    {
        Previous = 0,
        Unaltered = 1,
        Next = 2,
        Combined = 3
    }
}
