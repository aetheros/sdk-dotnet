import Avatar from '@material-ui/core/Avatar';
import { blue, grey } from '@material-ui/core/colors';
import Drawer from '@material-ui/core/Drawer';
import Icon from '@material-ui/core/Icon';
import List from '@material-ui/core/List';
import ListItem from '@material-ui/core/ListItem';
import ListItemIcon from '@material-ui/core/ListItemIcon';
import ListItemText from '@material-ui/core/ListItemText';
import { createStyles, Theme, WithStyles, withStyles } from '@material-ui/core/styles';
import { dotnetifyVM } from 'dotnetify';
import { RouteLink } from 'dotnetify/react';
import * as React from 'react';

const styles = (theme: Theme) => createStyles({
	drawer: {

	},
	drawerPaper: {
		backgroundColor: grey[800]
	},
	logo: {
		cursor: 'pointer',
		fontSize: 22,
		color: 'white',
		lineHeight: `64px`,
		fontWeight: 'lighter',
		backgroundImage: 'url(https://aetheros.com/wp-content/uploads/2020/04/AOS-No-Text-300x280.png)',
		backgroundRepeat: 'no-repeat',
		backgroundPositionX: 8,
		backgroundPositionY: 2,
		backgroundSize: '52px 52px',
		backgroundColor: blue[800],
		paddingLeft: 70,
		height: 56,
		width: 160
	},
	menuItem: {
		color: grey[200],
		fontSize: 14,
		width: '100%'
	},
	itemIcon: {
		color: grey[400]
	},
	avatarBox: {
		padding: '15px 0 15px 15px',
		backgroundColor: grey[300]
	},
	avatarIcon: {
		float: 'left',
		display: 'block',
		marginRight: 15,
		boxShadow: '0px 0px 0px 8px rgba(0,0,0,0.2)'
	},
	avatarName: {
		paddingTop: 8,
		display: 'block',
		color: 'black',
		fontSize: '24px',
		fontWeight: 600
	}
});

export type SidebarMenu = {
	Title: string;
	Icon: string;
	Route: string;
};

interface Props extends WithStyles<typeof styles> {
	menus: SidebarMenu[];
	userAvatarUrl: string;
	vm: dotnetifyVM;
	logoTitle: string;
	open: boolean;
	username: string;
};

class SidebarComponent extends React.Component<Props> {

	constructor(props: Props) {
		super(props);
	}

	render() {
		const classes = this.props.classes;

		return (
			<Drawer
				variant="persistent"
				className={classes.drawer}
				open={this.props.open}
				classes={{
					paper: classes.drawerPaper
				}}
			>
				<div className={classes.logo}>{this.props.logoTitle}</div>
				<div className={classes.avatarBox}>
					<Avatar src={this.props.userAvatarUrl} sizes="small" className={classes.avatarIcon} />
					<span className={classes.avatarName}>{this.props.username}</span>
				</div>
				<List>
					{this.props.menus.map((menu, index) => (
						<ListItem button key={index}>
							<ListItemIcon>
								<Icon className={classes.itemIcon}>{menu.Icon}</Icon>
							</ListItemIcon>
							<RouteLink vm={this.props.vm} route={menu.Route} className={classes.menuItem}>
								<ListItemText primary={menu.Title} />
							</RouteLink>
						</ListItem>
					))}
				</List>
			</Drawer>
		);
	}
}
export const Sidebar = Object.assign(withStyles(styles)(SidebarComponent), { name: '' });

