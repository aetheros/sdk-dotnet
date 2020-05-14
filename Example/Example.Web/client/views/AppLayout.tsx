import { createStyles, Theme, ThemeProvider, WithStyles, withStyles } from '@material-ui/core/styles';
import withWidth from '@material-ui/core/withWidth';
import dotnetify, { dotnetifyVM } from 'dotnetify';
import React from 'react';
import auth from '../auth';
import Header from '../components/Header';
import { Sidebar, SidebarMenu } from '../components/Sidebar';
import defaultTheme from '../styles/theme-default';

const shouldSidebarOpen = width => width !== 'sm';
const paddingLeftSidebar = 236;

const styles = (theme: Theme) => createStyles({
	header: {
		paddingLeft: 0,//sidebarOpen ? paddingLeftSidebar : 0
	},
	container: {
		margin: '80px 20px 20px 15px',
		paddingLeft: paddingLeftSidebar,//sidebarOpen && this.props.width !== 'sm' ? paddingLeftSidebar : 0
	}
});

interface Props extends WithStyles<typeof styles> {
	userAvatar?: string;
	userName?: string;
	width?: string;
};

type State = {
	sidebarOpen: boolean;
	Menus: SidebarMenu[];
	UserAvatar: any;
	UserName: string;
};

class AppLayout extends React.Component<Props, State> {

	constructor(props) {
		super(props);

		this.vm = dotnetify.react.connect('AppLayout', this, {
			headers: { Authorization: 'Bearer ' + auth.getAccessToken() },
			exceptionHandler: _ => auth.signOut()
		});
		this.vm.onRouteEnter = (path, template) => (template.Target = 'Content');

		this.state = {
			sidebarOpen: shouldSidebarOpen(props.width),
			Menus: [],
		} as State;
	}

	vm: dotnetifyVM;
	dispatch(state: any) { return dotnetifyVM; }
	componentWillUnmount() { this.vm.$destroy(); }

	componentDidUpdate(prevProps) {
		if (prevProps.width !== this.props.width)
			this.setState({ sidebarOpen: shouldSidebarOpen(this.props.width) });
	}
	handleSidebarToggle(evt: any) {
		this.setState({ sidebarOpen: !this.state.sidebarOpen });
	}

	render() {
		let { sidebarOpen, Menus, UserAvatar, UserName } = this.state;
		let userAvatarUrl = UserAvatar ? UserAvatar : null;

		return (
			<ThemeProvider theme={defaultTheme}>
				<div>
					<Header onSidebarToggle={this.handleSidebarToggle.bind(this)} />
					<Sidebar vm={this.vm} logoTitle="dotNetify" open={sidebarOpen} userAvatarUrl={userAvatarUrl} menus={Menus} username={UserName} />
					<div className={this.props.classes.container} id="Content" />
				</div>
			</ThemeProvider>
		);
	}
}
// className={this.props.classes.header}
// className={this.props.classes.container} 

//export default withWidth()(withStyles(styles)(AppLayout));
export default withStyles(styles)(withWidth()(AppLayout));
