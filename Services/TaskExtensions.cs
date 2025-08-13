using System.Threading.Tasks;
namespace Services;

static class TaskExtensions
{
    public static void TryWait(this Task t)
    {
        try { t.Wait(); }
        catch
        {
            /* ignore */
        }
    }
}