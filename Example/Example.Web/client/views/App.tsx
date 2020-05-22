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

	handleAuthenticated() {
		this.setState({ authenticated: true });
	}

	render(): JSX.Element {
		if (this.state.authenticated)
			return <AppLayout />;

		return <LoginPage onAuthenticated={this.handleAuthenticated.bind(this)} />;
	}
}


