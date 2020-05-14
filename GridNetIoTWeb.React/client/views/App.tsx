import * as React from "react";
import auth from "../auth";
import AppLayout from "./AppLayout";
import LoginPage from "./LoginPage";

type Props = {
	children?: any;
};

type State = {
	authenticated: boolean;
};

export default class App extends React.Component<Props, State> {
	constructor(props) {
		super(props);
		this.state = {
			authenticated: auth.hasAccessToken()
		};
	}

	render(): JSX.Element {
		if (this.state.authenticated)
			return <AppLayout />;

		const handleAuthenticated = () => this.setState({ authenticated: true });
		return <LoginPage onAuthenticated={handleAuthenticated} />;
	}
}
