import Button from '@material-ui/core/Button';
import Card from '@material-ui/core/Card';
import { createStyles, Theme, ThemeProvider, WithStyles, withStyles } from '@material-ui/core/styles';
import TextField from '@material-ui/core/TextField';
import * as React from 'react';
import auth from '../auth';
import defaultTheme from '../styles/theme-default';

const styles = (theme: Theme) => createStyles({
	loginContainer: {
		minWidth: 320,
		maxWidth: 400,
		height: 'auto',
		position: 'absolute',
		top: '20%',
		left: 0,
		right: 0,
		margin: 'auto'
	},
	paper: {
		padding: 20,
		overflow: 'auto'
	},
	loginBtn: {
		float: 'right'
	},
	logo: {
		width: 20,
		height: 20,
		marginRight: 6,
		display: 'inline-block'
	},
	textField: {
		margin: '1rem 0'
	},
	text: {
		color: '#333',
		fontWeight: 'bold',
		backgroundColor: 'transparent',
		verticalAlign: 'text-bottom'
	},
	error: { color: 'red' }
});

interface Props extends WithStyles<typeof styles> {
	onAuthenticated: Function;
};

type State = {
	user: string;
	password: string;
	error: string;
};

class LoginPage extends React.Component<Props, State> {
	constructor(props: Props) {
		super(props);

		this.state = { user: 'guest', password: 'dotnetify', error: null };
	}

	async handleLogin() {
		let { user, password, error } = this.state;

		this.setState({ error: null });
		try {
			await auth.signIn(user, password)
			this.props.onAuthenticated();
		} catch (error) {
			if (error.message == '400')
				this.setState({ error: 'Invalid password' });
			else
				this.setState({ error: error.message });
		}
	};

	render() {
		let { user, password, error } = this.state;
		const { onAuthenticated, classes } = this.props;

		return (
			<ThemeProvider theme={defaultTheme}>
				<div>
					<div className={classes.loginContainer}>
						<Card className={classes.paper}>
							<div>
								<img src="https://dotnetify.net/content/images/dotnetify-logo-small.png" className={classes.logo} />
								<span className={classes.text}>IoT .NET App</span>
							</div>
							<form>
								<TextField
									required
									className={classes.textField}
									label="User"
									fullWidth={true}
									value={user}
									onChange={event => this.setState({ user: event.target.value })}
								/>
								<br />
								<TextField
									required
									className={classes.textField}
									label="Password"
									fullWidth={true}
									type="password"
									value={password}
									onChange={event => this.setState({ password: event.target.value })}
								/>
								{error ? <div className={classes.error}>{error}</div> : null}
								<div>
									<span>
										<Button variant="contained" onClick={this.handleLogin.bind(this)} color="primary" className={classes.loginBtn}>
											Login
										</Button>
									</span>
								</div>
							</form>
						</Card>
					</div>
				</div>
			</ThemeProvider>
		);
	}
}
export default withStyles(styles)(LoginPage);
