import { WithStyles, withStyles } from '@material-ui/core';
import Divider from '@material-ui/core/Divider';
import Paper from '@material-ui/core/Paper';
import * as React from 'react';
import { globalStylesRecord } from '../styles/styles';

let sss = {
	navigation: {
		fontSize: 15,
		fontWeight: 'lighter',
		color: '#777',
		paddingBottom: 15,
		display: 'block'
	},
	title: {
		fontSize: 24,
		fontWeight: 'lighter',
		marginBottom: 20
	},
	paper: {
		padding: 30
	},
	clear: {
		clear: 'both'
	}
};

interface Props extends WithStyles<typeof globalStylesRecord> {
	title: string;
	navigation: string;
}

class BasePageComponent extends React.Component<Props> {

	constructor(props: Props) {
		super(props);
	}

	render() {
		const { classes } = this.props;
		return (
			<div>
				<span className={classes.navigation}>{this.props.navigation}</span>
				<Paper className={classes.paper}>
					<h3 className={classes.title}>{this.props.title}</h3>
					<Divider />
					{this.props.children}
					<div className={classes.clear} />
				</Paper>
			</div>
		);
	}
}
export default Object.assign(withStyles(globalStylesRecord)(BasePageComponent), { name: '' });
