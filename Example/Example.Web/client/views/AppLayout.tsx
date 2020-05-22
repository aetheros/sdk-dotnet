import { createStyles, Theme, ThemeProvider, WithStyles, withStyles } from '@material-ui/core/styles';
import withWidth from '@material-ui/core/withWidth';
import dotnetify, { dotnetifyVM } from 'dotnetify';
import React from 'react';
import auth from '../auth';
import Header from '../components/Header';
import { Sidebar, SidebarMenu } from '../components/Sidebar';
import defaultTheme from '../styles/theme-default';
import { Button } from '@material-ui/core';

const shouldSidebarOpen = width => width !== 'sm';
const paddingLeftSidebar = 236;

const styles = (theme: Theme) => createStyles({
	header: {
		paddingLeft: paddingLeftSidebar,
	},
	header_showSidebar: {
		paddingLeft: 0,
	},
	container: {
		margin: '80px 20px 20px 15px',
		paddingLeft: 0,
	},
	container_showSidebar: {
		paddingLeft: paddingLeftSidebar,
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
			headers: { Authorization: `Bearer ${auth.getAccessToken()}` },
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

		let showSidebar = sidebarOpen && this.props.width !== 'sm';

		return (
			<ThemeProvider theme={defaultTheme}>
				<div>
					<Header
						onSidebarToggle={this.handleSidebarToggle.bind(this)}
						classes={{
							root: sidebarOpen ? this.props.classes.header : this.props.classes.header_showSidebar
						}}
						title=""
					/>
					<Sidebar
						vm={this.vm}
						logoTitle="Aetheros"
						open={sidebarOpen}
						userAvatarUrl={userAvatarUrl}
						menus={Menus}
						username={UserName} />
					<div
						id="Content"
						ref={(dd) => {
							console.log(dd);
						}}
						className={`${this.props.classes.container} ${showSidebar && this.props.classes.container_showSidebar}`}
					/>
				</div>
			</ThemeProvider>
		);
	}
}
export default Object.assign(withStyles(styles)(AppLayout), { name: '' });