export async function refreshAccessToken() {
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