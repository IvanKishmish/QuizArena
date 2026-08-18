const API_URL = "http://localhost:5000";

export async function apiRequest(
    path: string,
    options?: RequestInit
) {
    return fetch(API_URL + path, {
        ...options,
        credentials: "include"
    });
}