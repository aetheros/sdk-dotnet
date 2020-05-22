import { ListItem, ListItemText } from "@material-ui/core";
import { cyan } from "@material-ui/core/colors";
import ListSubheader from '@material-ui/core/ListSubheader';
import Paper from "@material-ui/core/Paper";
import { createStyles, Theme, WithStyles, withStyles } from '@material-ui/core/styles';
import { dotnetifyVM } from "dotnetify";
import * as React from "react";
import { FixedSizeList } from "react-window";

const styles = (theme: Theme) => createStyles({
	subheader: {
		fontSize: 24,
		fontWeight: 'lighter',
		backgroundColor: cyan[600],
		color: "white"
	},
});

interface Props extends WithStyles<typeof styles> {
	data: MeterEvent[];
	vm: dotnetifyVM;
};

export type MeterEvent = {
	EventTime: string;
	EventType: string;
};

class MeterEventsComponent extends React.Component<Props> {

	constructor(props: Props) {
		super(props);
	}

	Row = ({ index, style }) => {
		const data = this.props.data[index];
		return (
			<ListItem key={index} style={style}>
				<ListItemText
					primary={data.EventType}
					secondary={data.EventTime}
				/>
			</ListItem>
		);
	};

	render() {
		return (
			<FixedSizeList
				dense={true}
				subheader={<ListSubheader>Events</ListSubheader>}
				height={100}
				itemCount={this.props.data.length}
				itemSize={50}
				width="100%"
			>
				{this.Row}
			</FixedSizeList>
		);
	}
};

export const MeterEvents = Object.assign(withStyles(styles)(MeterEventsComponent), { name: '' });