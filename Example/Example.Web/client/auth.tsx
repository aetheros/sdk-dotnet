class Auth {
	url = "/token";

	async signIn(username, password) {
		const response = await fetch(this.url, {
			method: 'post',
			mode: 'no-cors',
			body: new URLSearchParams({
				username: username,
				password: password,
				grant_type: "password",
				client_id: "dotnetifydemo",
			}),
		});

		if (!response.ok)
			throw new Error(response.status.toString());

		const token = await response.json();
		window.localStorage.setItem("access_token", token.access_token);
	}

	signOut() {
		window.localStorage.removeItem("access_token");
		window.location.href = "/";
	}

	getAccessToken() {
		return window.localStorage.getItem("access_token");
	}

	hasAccessToken() {
		return this.getAccessToken() != null;
	}
}

export default new Auth();