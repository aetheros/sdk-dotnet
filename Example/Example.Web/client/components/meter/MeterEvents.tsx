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

export const MeterEvents = withStyles(styles)(class MeterEventsComponent extends React.Component<Props> {

	constructor(props: Props) {
		super(props);
	}

	RowContent = (index) => {
		return (
			<ListItem>
				<ListItemText
					primary={this.props.data[index].EventType}
					secondary={this.props.data[index].EventTime}
				/>
			</ListItem>
		);
	};

	Row = ({ index, style }) => {
		return (
			<div key={index} style={style}>
				{this.RowContent(index)}
			</div>
		);
	};

	content() {
		if (this.props.data && this.props.data.length > 0) {
			return (
				<FixedSizeList
					height={340}
					itemCount={this.props.data.length}
					itemSize={50}
					width="100%"
				>
					{this.Row}
				</FixedSizeList>
			);
		}
		else {
			return (
				<span>No events found</span>
			);
		}
	}

	render() {
		return (
			<Paper>
				<ListSubheader>Events</ListSubheader>
				{this.content()}
			</Paper>
		);
	}
});
