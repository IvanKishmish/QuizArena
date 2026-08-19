let refreshPromise: Promise<string | null> | null = null;

async function performRefresh() {
    const response = await fetch(
        "http://localhost:5000/api/Auth/refresh",
        {
            method: "POST",
            credentials: "include",
        }
    );

    if (!response.ok) {
        return null;
    }

    const data = await response.json();

    return data.accessToken;
}
export async function refreshAccessToken() {
    if (refreshPromise) {
        return refreshPromise;
    } 
    refreshPromise = performRefresh();
    try {
        return await refreshPromise;
    } finally {
        refreshPromise = null;
    }
}