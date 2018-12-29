namespace Timelapse.Enums
{
    // Possible ways that an image is expected to be rendered
    public enum ImageDisplayIntentEnum
    {
        TransientLoading,    // Indicates Timelapse is loading images, and providing feedback by rapidly showing each image
        TransientNavigating, // Indicates the user is navigating images quickly (e.g., arrow keys, slider), where images are shown briefly
        Persistent           // Indicates the image will likely be on display for more than a brief moment.
    }
}
